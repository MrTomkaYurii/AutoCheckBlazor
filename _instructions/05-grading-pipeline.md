# Grading Pipeline

Уся логіка — в `Services/GradingService.cs` (одна `RunAsync`, без окремих step-класів).
Сервер **клонує** репо студента локально й аналізує код через **Gemini**. Студент
здає конкретний коміт, не «поточний стан гілки».

## Флоу здачі

```
1. Студент відкриває лабу → "Здати"
   ├─ GitHub API тягне гілки/коміти репозиторію
   ├─ Режим "Список комітів" або "Граф гіта"
   └─ Маппінг коміт → завдання (CommitMappingJson: sha → taskNumber)

2. GradingService.RunAsync(submissionId, studentId):

   ── 0. Pre-flight (кидає, якщо не так — спроба НЕ витрачається) ──
   • AttemptsUsed < AttemptsMax
   • Deadline не минув (перевірка сервером, не лише disabled-кнопкою)
   • Gemini денна квота не вичерпана (GeminiQuotaService)
   • Gemini:ApiKey заданий
   • Repo-substitution guard: FindSharedRepoAsync — якщо той самий репо в іншого
     студента → hard-fail (Rejected, defense=0, final=0, спроба витрачена), окрім
     PlagiarismApproved

   ── Черга ── GradingQueueService.EnterAsync — одна перевірка за раз;
      після очікування ліміт спроб і дедлайн перевіряються ЩЕ раз

   ── 1. git ── PrepareRepoAsync: clone (або set-url+fetch+reset) у WorkRoot/{hash};
      private-репо клонуються через розшифрований токен (x-access-token)

   ── 2. Витяг коду per-task ──
   • є маппінг → git show {sha} --unified=5, фільтр хунків файлу taskN
   • нема маппінгу → евристичний пошук файлу по назві завдання
   • + вимоги з checks.json (LoadTaskChecks)

   ── 2.5 Plagiarism-гейт ── FindExactMatchAsync (ДО Gemini, квота не витрачається):
      повний збіг рядків з уже перевіреною роботою → Rejected + PlagiarismFlag

   ── 3. Gemini ── GradeAllWithGeminiAsync: ОДИН batch-запит на всі завдання,
      responseMimeType=application/json, temperature 0.1; 429 → чекає 30с і ретрай

   ── 4. Оцінка ──
      score_task = done/(done+issues) × 100      (число від Gemini ігнорується — прозорість)
      state = pass ≥80 / warn ≥50 / fail
      AutoScore = Scoring.Weighted(score, difficulty) — найкраща з усіх спроб
      ≥ 50 → Status = Review; інакше AutoScore = null, Status = Rejected

   ── 5. Persist ── TaskResult (+DiffLines) на кожне завдання, з AttemptNo;
      нотифікація студенту
```

## Чому системний збій не витрачає спробу
`GradeAllWithGeminiAsync` кидає `InvalidOperationException` («систему недоступно…»),
коли API впав, повернув не-масив або пропустив завдання. Спробу списує лише
**реальний** результат перевірки (або підтверджений плагіат/підміна репо).

## checks.json — вимоги на завдання
Формат — перелік текстових **вимог** (не I/O-кейси). Gemini перевіряє кожну й
розкладає у `done` / `issues`:

```json
{
  "tasks": [
    {
      "n": 1,
      "requirements": [
        "Простір імен: namespace ClinicApp; (файловий стиль)",
        "Поле private static int _nextId = 1;",
        "Властивість Id: public int Id { get; } — лише гетер",
        "..."
      ]
    }
  ]
}
```

- `n` — номер завдання (= `LabTask.Number`)
- `requirements[]` — конкретні перевірні вимоги; якщо блок є, Gemini перевіряє ТІЛЬКИ їх
- заповнені для всіх 22 лаб

## Промпт до Gemini (стисло)
Роль «суворий, але справедливий перевірник C#»; для кожного завдання — заголовок,
складність (⭐), Brief, вимоги з checks.json, код студента. Відповідь — валідний
JSON-масив `[{ n, done[], issues[], analysis }]` рівно з N елементів.

## Кеш репозиторіїв
Клони кешуються в `Grading:WorkRoot` (порожньо → temp) за хешем URL і переиспользуються.
`RepoCleanupService` щодня видаляє ті, що простоюють > `Grading:RepoRetentionDays`
(default 7) — видалений клон просто переклонується наступного разу.

## Чого НЕ перевіряємо
- Назви класів/методів — у студентів різні домени (клініка, готель, ресторан)
- Внутрішню структуру — тільки виконання вимог із checks.json
- `TestsPassed/TestsTotal` наразі 0 — окремі xUnit-тести (lab-01) не підключені до пайплайну
