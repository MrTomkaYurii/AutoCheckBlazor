<!DOCTYPE html>
<html lang="uk">
<head>
    <meta charset="UTF-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
    <title>AutoCheck · Новий пароль</title>
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

            <!-- Icon + heading -->
            <div style="display:flex;flex-direction:column;align-items:center;text-align:center;margin-bottom:28px">
                <div style="width:56px;height:56px;border-radius:16px;display:grid;place-items:center;background:rgba(118,199,173,0.1);border:1px solid rgba(118,199,173,0.25);margin-bottom:16px">
                    <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="var(--acc)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <rect width="18" height="11" x="3" y="11" rx="2" ry="2"/>
                        <path d="M7 11V7a5 5 0 0 1 10 0v4"/>
                        <path d="M12 16v2" stroke-width="2.5"/>
                    </svg>
                </div>
                <h2 style="margin:0 0 6px;font-size:22px;font-weight:730;letter-spacing:-0.025em">Новий пароль</h2>
                <p style="margin:0;color:var(--tx-3);font-size:13.5px;max-width:300px;line-height:1.5">
                    Введіть новий пароль для вашого акаунта
                </p>
            </div>

            <!-- Alert -->
            <#if message?has_content>
            <div class="ac-alert ac-alert-${message.type}" style="margin-bottom:16px">
                ${kcSanitize(message.summary)?no_esc}
            </div>
            </#if>

            <!-- Update password form -->
            <form action="${url.loginAction}" method="post" class="ac-form">

                <#if isAppInitiatedAction??>
                <input type="hidden" name="logout-sessions" value="on"/>
                </#if>

                <div class="ac-field">
                    <label class="ac-label" for="password-new">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
                        Новий пароль
                    </label>
                    <div class="ac-input-wrap">
                        <span class="ac-input-lead">
                            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
                        </span>
                        <input tabindex="1" id="password-new" name="password-new" type="password"
                               class="ac-input" style="padding-right:44px!important"
                               placeholder="••••••••" autofocus autocomplete="new-password"/>
                        <button type="button" class="ac-eye" id="eye-password-new" onclick="togglePwd('password-new','eye-password-new')">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>
                        </button>
                    </div>
                </div>

                <div class="ac-field">
                    <label class="ac-label" for="password-confirm">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/><polyline points="9 16 11 18 15 14"/></svg>
                        Підтвердження пароля
                    </label>
                    <div class="ac-input-wrap">
                        <span class="ac-input-lead">
                            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
                        </span>
                        <input tabindex="2" id="password-confirm" name="password-confirm" type="password"
                               class="ac-input" style="padding-right:44px!important"
                               placeholder="••••••••" autocomplete="new-password"/>
                        <button type="button" class="ac-eye" id="eye-password-confirm" onclick="togglePwd('password-confirm','eye-password-confirm')">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>
                        </button>
                    </div>
                </div>

                <button tabindex="3" type="submit" class="ac-btn-submit" style="margin-top:6px">
                    Зберегти пароль
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
                </button>

                <#if isAppInitiatedAction??>
                <button tabindex="4" type="submit" name="cancel-aia" value="true" class="ac-btn-submit"
                        style="margin-top:8px;background:transparent;border:1px solid rgba(118,199,173,0.25);color:var(--tx-3)">
                    Скасувати
                </button>
                </#if>

            </form>

        </div>
    </div>
</div>

<script>
var eyeIconOpen = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>';
var eyeIconClosed = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9.88 9.88a3 3 0 1 0 4.24 4.24"/><path d="M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68"/><path d="M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61"/><line x1="2" x2="22" y1="2" y2="22"/></svg>';
function togglePwd(inputId, btnId) {
    var input = document.getElementById(inputId);
    var isHidden = input.type === 'password';
    input.type = isHidden ? 'text' : 'password';
    document.getElementById(btnId).innerHTML = isHidden ? eyeIconClosed : eyeIconOpen;
}
</script>

</body>
</html>
