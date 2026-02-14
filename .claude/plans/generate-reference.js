const fs = require('fs');
const path = require('path');

const BASE = 'C:/Users/imasa/github/tsqlrefine';
const RULES_DIR = path.join(BASE, 'src/TsqlRefine.Rules/Rules');
const RULESETS_DIR = path.join(BASE, 'rulesets');

// 1. Load all presets in tier order
const presets = [
  { file: 'security-only.json', tier: 'Critical', label: 'security-only', desc: 'Security vulnerabilities and critical safety issues that can cause data loss or security breaches. These rules should never be disabled in production code.' },
  { file: 'pragmatic.json', tier: 'Essential', label: 'pragmatic', desc: 'Production-ready minimum for correctness and preventing runtime errors. Fundamental checks that catch bugs before they reach production.' },
  { file: 'recommended.json', tier: 'Recommended', label: 'recommended', desc: 'Balanced production use with semantic analysis and best practices. This is the default preset, providing comprehensive validation without excessive noise.' },
  { file: 'strict-logic.json', tier: 'Thorough', label: 'strict-logic', desc: 'Comprehensive correctness, performance, and schema checks without cosmetic style enforcement.' },
  { file: 'strict.json', tier: 'Cosmetic', label: 'strict', desc: 'Style consistency, formatting, and naming conventions for maximum code uniformity.' },
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

function extractBalancedParens(text, startIdx) {
  let depth = 0;
  let inString = false;
  let escapeNext = false;
  let i = startIdx;
  while (i < text.length) {
    const ch = text[i];
    if (escapeNext) { escapeNext = false; i++; continue; }
    if (ch === '\\' && inString) { escapeNext = true; i++; continue; }
    if (ch === '"' && !inString) { inString = true; i++; continue; }
    if (ch === '"' && inString) { inString = false; i++; continue; }
    if (!inString) {
      if (ch === '(') depth++;
      else if (ch === ')') { depth--; if (depth === 0) return text.substring(startIdx + 1, i); }
    }
    i++;
  }
  return null;
}

const ruleFiles = findRuleFiles(RULES_DIR);
const rules = [];

for (const filePath of ruleFiles) {
  const content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  const metaStart = content.match(/RuleMetadata\s+Metadata\s*\{[^}]*\}\s*=\s*new\s*\(/);
  if (!metaStart) continue;

  const openParenIdx = metaStart.index + metaStart[0].length - 1;
  const block = extractBalancedParens(content, openParenIdx);
  if (!block) continue;

  // Extract RuleId
  const ruleIdMatch = block.match(/RuleId:\s*(?:"([^"]+)"|(\w+))/);
  let ruleId = null;
  if (ruleIdMatch) {
    if (ruleIdMatch[1]) ruleId = ruleIdMatch[1];
    else {
      const constMatch = content.match(new RegExp('const\\s+string\\s+' + ruleIdMatch[2] + '\\s*=\\s*"([^"]+)"'));
      ruleId = constMatch ? constMatch[1] : ruleIdMatch[2];
    }
  }
  if (!ruleId) continue;

  // Extract Description
  let description = null;
  const descIdx = block.indexOf('Description:');
  if (descIdx !== -1) {
    const afterDesc = block.substring(descIdx + 'Description:'.length).trimStart();
    if (afterDesc.startsWith('"')) {
      let end = 1;
      while (end < afterDesc.length) {
        if (afterDesc[end] === '\\') { end += 2; continue; }
        if (afterDesc[end] === '"') break;
        end++;
      }
      description = afterDesc.substring(1, end);
    } else {
      const constNameMatch = afterDesc.match(/^(\w+)/);
      if (constNameMatch) {
        const constMatch = content.match(new RegExp('const\\s+string\\s+' + constNameMatch[1] + '\\s*=\\s*"((?:[^"\\\\]|\\\\.)*)"'));
        description = constMatch ? constMatch[1] : constNameMatch[1];
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
        const constMatch = content.match(new RegExp('const\\s+string\\s+' + constNameMatch[1] + '\\s*=\\s*"([^"]+)"'));
        category = constMatch ? constMatch[1] : constNameMatch[1];
      }
    }
  }

  const sevMatch = block.match(/DefaultSeverity:\s*RuleSeverity\.(\w+)/);
  const severity = sevMatch ? sevMatch[1] : 'Unknown';

  const fixMatch = block.match(/Fixable:\s*(true|false)/);
  const fixable = fixMatch ? fixMatch[1] === 'true' : false;

  const tier = tierMap.get(ruleId) || 'UNASSIGNED';
  const docCategory = (category || 'unknown').toLowerCase();
  const docLink = docCategory + '/' + ruleId + '.md';

  // Display name: semantic rules show as semantic/xxx
  let displayName = ruleId;
  if (ruleId.startsWith('semantic-')) {
    displayName = 'semantic/' + ruleId.substring('semantic-'.length);
  }

  rules.push({
    tier,
    category: category || 'Unknown',
    ruleId,
    displayName,
    description: description || '',
    severity,
    fixable,
    docLink,
  });
}

// Sort by tier, category, ruleId
const tierOrder = { Critical: 0, Essential: 1, Recommended: 2, Thorough: 3, Cosmetic: 4, UNASSIGNED: 5 };
const categoryOrder = ['Security', 'Safety', 'Correctness', 'Performance', 'Transactions', 'Schema', 'Style', 'Debug'];
rules.sort((a, b) => {
  const t = (tierOrder[a.tier] ?? 99) - (tierOrder[b.tier] ?? 99);
  if (t !== 0) return t;
  const ca = categoryOrder.indexOf(a.category);
  const cb = categoryOrder.indexOf(b.category);
  const c = (ca === -1 ? 99 : ca) - (cb === -1 ? 99 : cb);
  if (c !== 0) return c;
  return a.ruleId.localeCompare(b.ruleId);
});

// Statistics
const totalRules = rules.length;
const fixableCount = rules.filter(r => r.fixable).length;
const fixablePct = Math.round(fixableCount / totalRules * 100);

const tierCounts = {};
for (const r of rules) tierCounts[r.tier] = (tierCounts[r.tier] || 0) + 1;

const catCounts = {};
for (const r of rules) catCounts[r.category] = (catCounts[r.category] || 0) + 1;

const sevCounts = {};
for (const r of rules) sevCounts[r.severity] = (sevCounts[r.severity] || 0) + 1;

// Category descriptions
const catDescs = {
  Correctness: 'Detects code that may produce incorrect results or runtime errors',
  Safety: 'Prevents destructive or dangerous operations',
  Security: 'Identifies security vulnerabilities like SQL injection',
  Performance: 'Flags patterns that can cause performance issues',
  Style: 'Maintains code formatting and consistency',
  Transactions: 'Ensures proper transaction handling and session settings',
  Schema: 'Enforces database schema best practices',
  Debug: 'Controls debug and output statements',
};

// Generate markdown
let md = '';

md += '# TsqlRefine Rules Reference\n\n';
md += '> NOTE: This file is generated automatically. Do not edit manually.\n';
md += '> For an overview and guide, see [README.md](README.md).\n\n';

// TOC
md += '## Table of Contents\n\n';
md += '- [Rule Statistics](#rule-statistics)\n';
md += '- [Importance Tiers](#importance-tiers)\n';
md += '- [Rule Categories](#rule-categories)\n';
md += '- [Rules by Importance Tier](#rules-by-importance-tier)\n';
for (const p of presets) {
  const anchor = p.tier.toLowerCase() + '-' + p.label.replace(/-/g, '');
  let anchorText;
  if (p.tier === 'Critical') anchorText = 'critical-security-only';
  else if (p.tier === 'Essential') anchorText = 'essential-pragmatic';
  else if (p.tier === 'Recommended') anchorText = 'recommended';
  else if (p.tier === 'Thorough') anchorText = 'thorough-strict-logic';
  else anchorText = 'cosmetic-strict';
  md += `  - [${p.tier} (${p.label})](#${anchorText})\n`;
}
md += '- [Rules by Severity](#rules-by-severity)\n';
md += '- [Fixable Rules](#fixable-rules)\n\n';

// Rule Statistics
md += '## Rule Statistics\n\n';
md += `- **Total Rules**: ${totalRules}\n`;
md += `- **Fixable Rules**: ${fixableCount} (${fixablePct}%)\n`;
md += '- **By Importance Tier**:\n';
for (const p of presets) {
  md += `  - ${p.tier} (${p.label}): ${tierCounts[p.tier] || 0} rules\n`;
}
md += '- **By Severity**:\n';
for (const sev of ['Error', 'Warning', 'Information']) {
  const cnt = sevCounts[sev] || 0;
  const pct = Math.round(cnt / totalRules * 100);
  md += `  - ${sev}: ${cnt} rules (${pct}%)\n`;
}
md += '\n';

// Importance Tiers
md += '## Importance Tiers\n\n';
md += 'Rules are organized into five importance tiers based on which preset first includes them. Each higher preset is a strict superset of the one below:\n\n';
md += '```\nsecurity-only ⊂ pragmatic ⊂ recommended ⊂ strict-logic ⊂ strict\n```\n\n';
md += '| Tier | Preset | Rules | Cumulative | Description |\n';
md += '|------|--------|-------|------------|-------------|\n';
let cumulative = 0;
for (const p of presets) {
  const cnt = tierCounts[p.tier] || 0;
  cumulative += cnt;
  const shortDesc = p.desc.split('.')[0];
  md += `| **${p.tier}** | ${p.label} | ${cnt} | ${cumulative} | ${shortDesc} |\n`;
}
md += '\n';

// Rule Categories
md += '## Rule Categories\n\n';
md += '| Category | Rules | Description |\n';
md += '|----------|-------|-------------|\n';
for (const cat of categoryOrder) {
  if (catCounts[cat]) {
    md += `| **${cat}** | ${catCounts[cat]} | ${catDescs[cat] || ''} |\n`;
  }
}
md += '\n';

// Rules by Importance Tier
md += '## Rules by Importance Tier\n\n';

for (const p of presets) {
  const tierRules = rules.filter(r => r.tier === p.tier);
  const cnt = tierRules.length;

  if (p.tier === 'Critical') md += `### Critical (security-only)\n\n`;
  else if (p.tier === 'Essential') md += `### Essential (pragmatic)\n\n`;
  else if (p.tier === 'Recommended') md += `### Recommended\n\n`;
  else if (p.tier === 'Thorough') md += `### Thorough (strict-logic)\n\n`;
  else md += `### Cosmetic (strict)\n\n`;

  md += `**${cnt} rules** — ${p.desc}\n\n`;

  // Group by category
  const cats = [...new Set(tierRules.map(r => r.category))];
  // Sort by categoryOrder
  cats.sort((a, b) => {
    const ia = categoryOrder.indexOf(a);
    const ib = categoryOrder.indexOf(b);
    return (ia === -1 ? 99 : ia) - (ib === -1 ? 99 : ib);
  });

  for (const cat of cats) {
    const catRules = tierRules.filter(r => r.category === cat);
    md += `#### ${cat} (${catRules.length} rules)\n\n`;
    md += '| Rule ID | Description | Severity | Fixable |\n';
    md += '|---------|-------------|----------|---------|\n';
    for (const r of catRules) {
      const fixStr = r.fixable ? '**Yes**' : 'No';
      md += `| [${r.displayName}](${r.docLink}) | ${r.description} | ${r.severity} | ${fixStr} |\n`;
    }
    md += '\n';
  }
}

// Rules by Severity
md += '## Rules by Severity\n\n';
for (const sev of ['Error', 'Warning', 'Information']) {
  const sevRules = rules.filter(r => r.severity === sev);
  md += `### ${sev} (${sevRules.length} rules)\n\n`;
  // Sort alphabetically by ruleId within severity
  const sorted = [...sevRules].sort((a, b) => a.ruleId.localeCompare(b.ruleId));
  for (const r of sorted) {
    md += `- [${r.displayName}](${r.docLink})\n`;
  }
  md += '\n';
}

// Fixable Rules
const fixableRules = rules.filter(r => r.fixable).sort((a, b) => a.ruleId.localeCompare(b.ruleId));
md += `## Fixable Rules\n\n`;
md += `The following ${fixableRules.length} rules support automatic fixing:\n\n`;
let i = 1;
for (const r of fixableRules) {
  md += `${i}. [${r.displayName}](${r.docLink}) - ${r.description}\n`;
  i++;
}
md += '\nTo apply auto-fixes, use the `fix` command:\n\n';
md += '```powershell\ndotnet run --project src/TsqlRefine.Cli -c Release -- fix --write file.sql\n```\n';

// Write output
const outPath = path.join(BASE, 'docs/Rules/REFERENCE.md');
fs.writeFileSync(outPath, md, 'utf8');
console.log('Generated ' + outPath);
console.log('Total rules: ' + totalRules);
console.log('Fixable: ' + fixableCount);
