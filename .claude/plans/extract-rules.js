const fs = require('fs');
const path = require('path');

const BASE = 'C:/Users/imasa/github/tsqlrefine';
const RULES_DIR = path.join(BASE, 'src/TsqlRefine.Rules/Rules');
const RULESETS_DIR = path.join(BASE, 'rulesets');

// 1. Load all presets in tier order
const presets = [
  { file: 'security-only.json', tier: 'Critical' },
  { file: 'pragmatic.json',     tier: 'Essential' },
  { file: 'recommended.json',   tier: 'Recommended' },
  { file: 'strict-logic.json',  tier: 'Thorough' },
  { file: 'strict.json',        tier: 'Cosmetic' },
];

// Build a map: ruleId -> first tier it appears in
const tierMap = new Map();
for (const preset of presets) {
  const data = JSON.parse(fs.readFileSync(path.join(RULESETS_DIR, preset.file), 'utf8'));
  for (const rule of data.rules) {
    if (!tierMap.has(rule.id)) {
      tierMap.set(rule.id, preset.tier);
    }
  }
}

// 2. Find all *Rule.cs files recursively
function findRuleFiles(dir) {
  let results = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results = results.concat(findRuleFiles(full));
    } else if (entry.name.endsWith('Rule.cs')) {
      results.push(full);
    }
  }
  return results;
}

const ruleFiles = findRuleFiles(RULES_DIR);

/**
 * Extract the balanced parenthesized block from `text` starting at `startIdx`
 * (which should point to the opening '(').
 * Returns the content inside the outer parens.
 */
function extractBalancedParens(text, startIdx) {
  let depth = 0;
  let inString = false;
  let escapeNext = false;
  let i = startIdx;

  while (i < text.length) {
    const ch = text[i];

    if (escapeNext) {
      escapeNext = false;
      i++;
      continue;
    }

    if (ch === '\\' && inString) {
      escapeNext = true;
      i++;
      continue;
    }

    if (ch === '"' && !inString) {
      inString = true;
      i++;
      continue;
    }

    if (ch === '"' && inString) {
      inString = false;
      i++;
      continue;
    }

    if (!inString) {
      if (ch === '(') {
        depth++;
      } else if (ch === ')') {
        depth--;
        if (depth === 0) {
          return text.substring(startIdx + 1, i);
        }
      }
    }

    i++;
  }

  return null;
}

// 3. Extract metadata from each rule file
const rules = [];

for (const filePath of ruleFiles) {
  const content = fs.readFileSync(filePath, 'utf8');
  // Normalize line endings
  const normalized = content.replace(/\r\n/g, '\n').replace(/\r/g, '\n');

  // Find the metadata property and the start of its constructor call
  const metaStart = normalized.match(/RuleMetadata\s+Metadata\s*\{[^}]*\}\s*=\s*new\s*\(/);
  if (!metaStart) {
    console.error('WARNING: No RuleMetadata found in ' + filePath);
    continue;
  }

  // Find the opening paren position
  const openParenIdx = metaStart.index + metaStart[0].length - 1;
  const block = extractBalancedParens(normalized, openParenIdx);

  if (!block) {
    console.error('WARNING: Could not extract balanced parens in ' + filePath);
    continue;
  }

  // Extract RuleId - handle both inline strings and constant references
  const ruleIdMatch = block.match(/RuleId:\s*(?:"([^"]+)"|(\w+))/);
  let ruleId = null;
  if (ruleIdMatch) {
    if (ruleIdMatch[1]) {
      ruleId = ruleIdMatch[1];
    } else {
      const constName = ruleIdMatch[2];
      const constMatch = normalized.match(new RegExp('const\\s+string\\s+' + constName + '\\s*=\\s*"([^"]+)"'));
      ruleId = constMatch ? constMatch[1] : constName;
    }
  }

  // Extract Description
  // Use a more robust approach: find "Description:" then grab the string value
  let description = null;
  const descIdx = block.indexOf('Description:');
  if (descIdx !== -1) {
    const afterDesc = block.substring(descIdx + 'Description:'.length).trimStart();
    if (afterDesc.startsWith('"')) {
      // Inline string - extract until closing unescaped quote
      let end = 1;
      while (end < afterDesc.length) {
        if (afterDesc[end] === '\\') {
          end += 2;
          continue;
        }
        if (afterDesc[end] === '"') {
          break;
        }
        end++;
      }
      description = afterDesc.substring(1, end);
    } else {
      // Constant reference
      const constNameMatch = afterDesc.match(/^(\w+)/);
      if (constNameMatch) {
        const constName = constNameMatch[1];
        const constMatch = normalized.match(new RegExp('const\\s+string\\s+' + constName + '\\s*=\\s*"((?:[^"\\\\]|\\\\.)*)"'));
        description = constMatch ? constMatch[1] : constName;
      }
    }
  }

  // Extract Category
  let category = null;
  const catIdx = block.indexOf('Category:');
  if (catIdx !== -1) {
    const afterCat = block.substring(catIdx + 'Category:'.length).trimStart();
    if (afterCat.startsWith('"')) {
      const endQuote = afterCat.indexOf('"', 1);
      category = afterCat.substring(1, endQuote);
    } else {
      const constNameMatch = afterCat.match(/^(\w+)/);
      if (constNameMatch) {
        const constName = constNameMatch[1];
        const constMatch = normalized.match(new RegExp('const\\s+string\\s+' + constName + '\\s*=\\s*"([^"]+)"'));
        category = constMatch ? constMatch[1] : constName;
      }
    }
  }

  // Extract DefaultSeverity
  const sevMatch = block.match(/DefaultSeverity:\s*RuleSeverity\.(\w+)/);
  const severity = sevMatch ? sevMatch[1] : 'Unknown';

  // Extract Fixable
  const fixMatch = block.match(/Fixable:\s*(true|false)/);
  const fixable = fixMatch ? fixMatch[1] === 'true' : false;

  if (!ruleId) {
    console.error('WARNING: Could not extract RuleId from ' + filePath);
    continue;
  }

  // Determine tier from preset membership
  const tier = tierMap.get(ruleId) || 'UNASSIGNED';

  // Build DocLink: category(lowercase)/rule-id.md
  const docCategory = category ? category.toLowerCase() : 'unknown';
  const docLink = docCategory + '/' + ruleId + '.md';

  rules.push({
    tier,
    category: category || 'Unknown',
    ruleId,
    description: description || '',
    severity,
    fixable: fixable ? 'Yes' : 'No',
    docLink,
  });
}

// 4. Sort: tier order, then category, then ruleId
const tierOrder = { Critical: 0, Essential: 1, Recommended: 2, Thorough: 3, Cosmetic: 4, UNASSIGNED: 5 };
rules.sort((a, b) => {
  const t = (tierOrder[a.tier] !== undefined ? tierOrder[a.tier] : 99) - (tierOrder[b.tier] !== undefined ? tierOrder[b.tier] : 99);
  if (t !== 0) return t;
  const c = a.category.localeCompare(b.category);
  if (c !== 0) return c;
  return a.ruleId.localeCompare(b.ruleId);
});

// 5. Output TSV
console.log(['Tier', 'Category', 'RuleId', 'Description', 'Severity', 'Fixable', 'DocLink'].join('\t'));
for (const r of rules) {
  console.log([r.tier, r.category, r.ruleId, r.description, r.severity, r.fixable, r.docLink].join('\t'));
}

// 6. Summary to stderr
console.error('');
console.error('--- Summary ---');
console.error('Total rules parsed: ' + rules.length);

const tierCounts = {};
for (const r of rules) {
  tierCounts[r.tier] = (tierCounts[r.tier] || 0) + 1;
}
console.error('');
console.error('By Tier:');
for (const tier of ['Critical', 'Essential', 'Recommended', 'Thorough', 'Cosmetic', 'UNASSIGNED']) {
  if (tierCounts[tier]) {
    console.error('  ' + tier + ': ' + tierCounts[tier]);
  }
}

const catCounts = {};
for (const r of rules) {
  catCounts[r.category] = (catCounts[r.category] || 0) + 1;
}
console.error('');
console.error('By Category:');
for (const [cat, count] of Object.entries(catCounts).sort()) {
  console.error('  ' + cat + ': ' + count);
}

const fixCount = rules.filter(r => r.fixable === 'Yes').length;
console.error('');
console.error('Fixable rules: ' + fixCount + ' / ' + rules.length);

// 7. Verify: check for rules in presets that weren't found in code
console.error('');
console.error('--- Verification ---');
const foundIds = new Set(rules.map(r => r.ruleId));
let missingFromCode = 0;
for (const [id, tier] of tierMap.entries()) {
  if (!foundIds.has(id)) {
    console.error('MISSING from code: ' + id + ' (in ' + tier + ' preset)');
    missingFromCode++;
  }
}
if (missingFromCode === 0) {
  console.error('All preset rule IDs found in code. OK');
}

let notInPreset = 0;
for (const r of rules) {
  if (r.tier === 'UNASSIGNED') {
    console.error('NOT IN ANY PRESET: ' + r.ruleId + ' (' + r.category + ')');
    notInPreset++;
  }
}
if (notInPreset === 0) {
  console.error('All code rules assigned to a preset tier. OK');
}

let unknownCat = 0;
for (const r of rules) {
  if (r.category === 'Unknown') {
    console.error('UNKNOWN CATEGORY: ' + r.ruleId);
    unknownCat++;
  }
}
if (unknownCat === 0) {
  console.error('All rules have valid categories. OK');
}
