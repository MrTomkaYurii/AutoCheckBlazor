# Keycloak

## Запуск

```bash
docker compose up -d
```

Keycloak доступний на http://localhost:8080
Admin: `admin` / `admin`

## Realm autocheck

Конфіг: `keycloak/realm-autocheck.json`

Ключові налаштування:
- `loginTheme: "autocheck"` — кастомна тема
- `registrationAllowed: true`
- `resetPasswordAllowed: true`
- `resetCredentialsFlow: "reset credentials"` — flow скидання пароля

## Тестові акаунти

| Email | Пароль | Роль |
|-------|--------|------|
| teacher@test.com | Test1234! | teacher |
| student@test.com | Test1234! | student |
| student2@test.com | Test1234! | student |

## User Profile attributes

- `group` — академічна група (вибирається при реєстрації)
- `github` — GitHub репозиторій
- `requestedRole` — яку роль запитує (teacher/student)

## Client autocheck-blazor

- `clientId: autocheck-blazor`
- `secret: autocheck-secret`
- `redirectUris: http://localhost:5186/*`
- Mapper `realm-roles-mapper` → ролі в JWT як claim `roles`

## OIDC в Blazor (Program.cs)

```csharp
opt.Authority = "http://localhost:8080/realms/autocheck";
opt.ClientId = "autocheck-blazor";
opt.SaveTokens = true;
opt.GetClaimsFromUserInfoEndpoint = true;
// id_token_hint при logout (вимога Keycloak 24)
```

## Кастомна тема

Тека: `keycloak/themes/autocheck/login/`

Монтується як Docker volume:
```yaml
- ./keycloak/themes/autocheck:/opt/keycloak/themes/autocheck
```

### FTL файли

| Файл | Сторінка |
|------|----------|
| `login.ftl` | Вхід (вибір ролі, вкладки Вхід/Реєстрація) |
| `register.ftl` | Реєстрація (поля групи, GitHub, ролі) |
| `login-reset-password.ftl` | Скидання пароля — крок 1: введення email |
| `login-update-password.ftl` | Скидання пароля — крок 2: новий пароль |

**Назви шаблонів у Keycloak 24:**
- `login-reset-password.ftl` (не `login-reset-credentials.ftl` як у старших версіях!)
- `login-update-password.ftl`

### theme.properties

```properties
parent=keycloak
styles=css/login.css
cacheTemplates=false
cacheThemes=false
```

`cacheThemes=false` + `cacheTemplates=false` → зміни в FTL файлах підхоплюються одразу без перезапуску.

### FreeMarker синтаксис (важливо)

Правильно: `${kcSanitize(message.summary)?no_esc}`
Неправильно: `${message.summary?html}` — `?html` не підтримується в Keycloak FreeMarker конфігурації

## Зміни конфігу realm

Зміни в `.ftl` файлах — підхоплюються одразу (volume).

Зміни в `realm-autocheck.json` — треба перестворити volume:
```bash
docker compose down -v
docker compose up -d
```

## AuthService.EnsureLinkedAsync() — деталі

При першому вході або зміні Keycloak sub:
1. Шукає `UserLink` по `KeycloakSub`
2. Якщо знайдено → виходить
3. Якщо ні → шукає студента по email
4. Якщо студент знайдений і вже має `UserLink` (старий sub після скидання volume):
   - **Оновлює** `KeycloakSub` на новий — не створює дублікат
5. Якщо студента немає → `CreateStudentAsync()` + `Submission(Locked)` для всіх лаб
6. Race condition → `catch DbUpdateException` → `ChangeTracker.Clear()`
