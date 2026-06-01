<!DOCTYPE html>
<html lang="uk">
<head>
    <meta charset="UTF-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
    <title>AutoCheck · Вхід</title>
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

            <!-- Role switcher -->
            <div style="margin-bottom:16px">
                <div class="ac-role-label">Я входжу як</div>
                <div class="ac-role-grid">
                    <button type="button" class="ac-role-btn active" id="role-student" onclick="setRole('student')">
                        <span class="ac-role-icon">
                            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="8" r="5"/><path d="M20 21a8 8 0 1 0-16 0"/></svg>
                        </span>
                        <span style="font-size:14px;font-weight:650;color:var(--tx-1)">Студент</span>
                        <span class="ac-role-check">
                            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="var(--acc)" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
                        </span>
                    </button>
                    <button type="button" class="ac-role-btn" id="role-teacher" onclick="setRole('teacher')">
                        <span class="ac-role-icon">
                            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
                        </span>
                        <span style="font-size:14px;font-weight:650">Викладач</span>
                        <span class="ac-role-check">
                            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="var(--acc)" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
                        </span>
                    </button>
                </div>
            </div>

            <!-- Tab switcher -->
            <div class="ac-tabs">
                <span class="ac-tab active">Вхід</span>
                <a href="${url.registrationUrl}" class="ac-tab">Реєстрація</a>
            </div>

            <!-- Heading -->
            <div class="ac-heading">
                <h2>З поверненням 👋</h2>
                <p id="ac-subtitle">Увійдіть, щоб продовжити навчання</p>
            </div>

            <!-- Alert -->
            <#if message?has_content>
            <div class="ac-alert ac-alert-${message.type}">
                ${kcSanitize(message.summary)?no_esc}
            </div>
            </#if>

            <!-- Login form -->
            <form action="${url.loginAction}" method="post" class="ac-form">
                <input type="hidden" id="id-hidden-input" name="credentialId"
                    <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if>/>

                <div class="ac-field">
                    <label class="ac-label" for="username">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>
                        Email
                    </label>
                    <div class="ac-input-wrap">
                        <span class="ac-input-lead">
                            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>
                        </span>
                        <input tabindex="1" id="username" name="username" type="text"
                               class="ac-input"
                               value="${(login.username!'')}"
                               placeholder="student@test.com"
                               autofocus autocomplete="off"/>
                    </div>
                </div>

                <div class="ac-field">
                    <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:7px">
                        <label class="ac-label" for="password" style="margin-bottom:0">
                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
                            Пароль
                        </label>
                        <#if realm.resetPasswordAllowed>
                        <a tabindex="5" href="${url.loginResetCredentialsUrl}" class="ac-link" style="font-size:12.5px">Забули пароль?</a>
                        </#if>
                    </div>
                    <div class="ac-input-wrap">
                        <span class="ac-input-lead">
                            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
                        </span>
                        <input tabindex="2" id="password" name="password" type="password"
                               class="ac-input" style="padding-right:44px!important"
                               placeholder="••••••••" autocomplete="off"/>
                        <button type="button" class="ac-eye" id="eye-password" onclick="togglePwd('password','eye-password')">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>
                        </button>
                    </div>
                </div>

                <#if realm.rememberMe && !usernameEditDisabled??>
                <div style="margin-top:-4px">
                    <label style="display:flex;align-items:center;gap:8px;cursor:pointer;font-size:13px;color:var(--tx-3)">
                        <input tabindex="3" type="checkbox" name="rememberMe"
                               <#if login.rememberMe??>checked</#if>
                               style="accent-color:var(--acc);width:15px;height:15px"/>
                        Запам'ятати мене
                    </label>
                </div>
                </#if>

                <button tabindex="4" type="submit" class="ac-btn-submit" style="margin-top:6px">
                    Увійти
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4"/><polyline points="10 17 15 12 10 7"/><line x1="15" y1="12" x2="3" y2="12"/></svg>
                </button>
            </form>

            <div class="ac-switch">
                Немає акаунта?&nbsp;<a href="${url.registrationUrl}" class="ac-link">Зареєструватися</a>
            </div>

        </div>
    </div>
</div>

<script>
var loginSubtitles = { student: 'Увійдіть, щоб продовжити навчання', teacher: 'Увійдіть до панелі викладача' };
function setRole(role) {
    document.getElementById('role-student').classList.toggle('active', role === 'student');
    document.getElementById('role-teacher').classList.toggle('active', role === 'teacher');
    document.getElementById('ac-subtitle').textContent = loginSubtitles[role];
    try { sessionStorage.setItem('ac-role', role); } catch(e) {}
}
var eyeIconOpen = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></svg>';
var eyeIconClosed = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9.88 9.88a3 3 0 1 0 4.24 4.24"/><path d="M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68"/><path d="M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61"/><line x1="2" x2="22" y1="2" y2="22"/></svg>';
function togglePwd(inputId, btnId) {
    var input = document.getElementById(inputId);
    var isHidden = input.type === 'password';
    input.type = isHidden ? 'text' : 'password';
    document.getElementById(btnId).innerHTML = isHidden ? eyeIconClosed : eyeIconOpen;
}
try { var r = sessionStorage.getItem('ac-role'); if (r === 'teacher') setRole('teacher'); } catch(e) {}
</script>

</body>
</html>
