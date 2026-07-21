SELECT DATEADD(day, -1,
    DATEADD(month, 1,
        DATEADD(month, DATEDIFF(month, 0, @date), 0)));
