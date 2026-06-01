<!DOCTYPE html>
<html lang="uk">
<head>
    <meta charset="UTF-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
    <title>AutoCheck · Відновлення пароля</title>
    <link rel="preconnect" href="https://fonts.googleapis.com"/>
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous"/>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;600;700&display=swap" rel="stylesheet"/>
    <link rel="stylesheet" href="${url.resourcesPath}/css/login.css"/>
</head>
<body>

<div class="ac-page">

    <!-- ── Left brand panel ──────────────────────────────────────────────── -->
    <div class="ac-left">
        <pre class="ac-code-bg">public class Student : IGradable
{
    public string Name { get; init; }
    private List&lt;Lab&gt; _labs = new();

    public double Average() =&gt;
        _labs.Average(l =&gt; l.Final);
}</pre>

        <div>
            <div class="ac-logo">
                <div class="ac-logo-icon">{}</div>
                <div>
                    <div class="ac-logo-name">AutoCheck</div>
                    <div class="ac-logo-sub">C# OOP Course</div>
                </div>
            </div>
            <div class="ac-uni">
                Кафедра комп'ютерних наук<br/>
                <span style="color:rgba(174,187,181,0.7)">ЧНУ ім. Юрія Федьковича</span>
            </div>
        </div>

        <div class="ac-hero">
            <div class="ac-badge">Семестр 2 · 2025–2026</div>
            <h1 style="display:block!important">
                Об'єктно-орієнтоване<br/>програмування <span>на C#</span>
            </h1>
            <p>Здавайте лабораторні, отримуйте миттєву авто-перевірку коду та фідбек, відстежуйте прогрес до заліку — в одному місці.</p>
            <div class="ac-stats">
                <div><div class="ac-stat-val">22</div><div class="ac-stat-lbl">лабораторних</div></div>
                <div><div class="ac-stat-val">auto</div><div class="ac-stat-lbl">перевірка коду</div></div>
                <div><div class="ac-stat-val">GitHub</div><div class="ac-stat-lbl">інтеграція</div></div>
            </div>
        </div>

        <div class="ac-footer">© 2025–2026 · Кафедра КН · ЧНУ ім. Ю. Федьковича</div>
    </div>

    <!-- ── Right form panel ──────────────────────────────────────────────── -->
    <div class="ac-right">
        <div class="ac-card">

            <div style="display:flex;flex-direction:column;align-items:center;text-align:center;margin-bottom:28px">
                <div style="width:56px;height:56px;border-radius:16px;display:grid;place-items:center;background:rgba(118,199,173,0.1);border:1px solid rgba(118,199,173,0.25);margin-bottom:16px">
                    <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="var(--acc)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <rect width="18" height="11" x="3" y="11" rx="2" ry="2"/>
                        <path d="M7 11V7a5 5 0 0 1 10 0v4"/>
                    </svg>
                </div>
                <h2 style="margin:0 0 6px;font-size:22px;font-weight:730;letter-spacing:-0.025em">Відновлення пароля</h2>
                <p style="margin:0;color:var(--tx-3);font-size:13.5px;max-width:300px;line-height:1.5">
                    Введіть email або логін — надішлемо інструкцію для скидання пароля
                </p>
            </div>

            <#if message?has_content>
            <div class="ac-alert ac-alert-${message.type}" style="margin-bottom:16px">
                ${kcSanitize(message.summary)?no_esc}
            </div>
            </#if>

            <form id="kc-reset-password-form" action="${url.loginAction}" method="post" class="ac-form">

                <div class="ac-field">
                    <label class="ac-label" for="username">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>
                        Email або логін
                    </label>
                    <div class="ac-input-wrap">
                        <span class="ac-input-lead">
                            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>
                        </span>
                        <input tabindex="1" id="username" name="username" type="text"
                               class="ac-input"
                               placeholder="student@cnu.edu.ua"
                               autofocus autocomplete="off"/>
                    </div>
                </div>

                <button tabindex="2" type="submit" class="ac-btn-submit" style="margin-top:6px">
                    Надіслати інструкцію
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M22 2 11 13"/><path d="m22 2-7 20-4-9-9-4 20-7z"/></svg>
                </button>
            </form>

            <div class="ac-switch" style="margin-top:18px">
                <a href="${url.loginUrl}" class="ac-link" style="display:inline-flex;align-items:center;gap:6px">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
                    Повернутись до входу
                </a>
            </div>

        </div>
    </div>
</div>

</body>
</html>
