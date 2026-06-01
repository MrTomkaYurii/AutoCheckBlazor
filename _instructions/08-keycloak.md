# Keycloak

## Запуск

```bash
docker compose up -d
```

Keycloak доступний на http://localhost:8080

## Realm autocheck

Конфіг: `keycloak/realm-autocheck.json`

Налаштування:
- `registrationAllowed: true` — реєстрація дозволена
- `loginTheme: "autocheck"` — кастомна тема
- `resetPasswordAllowed: true`

Ролі:
- `teacher` — викладач
- `student` — студент

## Тестові акаунти

| Email | Пароль | Роль |
|-------|--------|------|
| teacher@test.com | Test1234! | teacher |
| student@test.com | Test1234! | student |
| student2@test.com | Test1234! | student |

## User Profile attributes

Визначені в `components.org.keycloak.userprofile.UserProfileProvider`:
- `group` — академічна група (вибирається при реєстрації)
- `github` — GitHub репозиторій
- `requestedRole` — яку роль запитує (teacher/student)

## Client autocheck-blazor

- ClientId: `autocheck-blazor`
- ClientSecret: `autocheck-secret`
- redirectUris: `https://localhost:7171/*`, `http://localhost:5186/*`
- Mapper `realm-roles-mapper` — додає ролі в JWT як claim `roles`

## OIDC в Blazor (Program.cs)

```csharp
.AddOpenIdConnect(opt => {
    opt.Authority = "http://localhost:8080/realms/autocheck";
    opt.ClientId = "autocheck-blazor";
    opt.SaveTokens = true;
    opt.GetClaimsFromUserInfoEndpoint = true;
    // Keycloak 24 вимагає id_token_hint при logout
    opt.Events.OnRedirectToIdentityProviderForSignOut = async ctx => {
        var idToken = await ctx.HttpContext.GetTokenAsync("id_token");
        if (!string.IsNullOrEmpty(idToken))
            ctx.ProtocolMessage.IdTokenHint = idToken;
    };
})
```

## Кастомна тема

Тека: `keycloak/themes/autocheck/login/`

Монтується в Docker як volume:
```yaml
volumes:
  - ./keycloak/themes/autocheck:/opt/keycloak/themes/autocheck
```

Файли:
- `login.ftl` — сторінка входу (FreeMarker template)
- `register.ftl` — сторінка реєстрації
- `resources/css/login.css` — стилі (dark glassmorphism)
- `theme.properties` — `parent=keycloak`

## Важливо при змінах

Зміни в `.ftl` файлах застосовуються **одразу** (volume монтований).
Зміни в `realm-autocheck.json` — треба видалити volume і перезапустити:
```bash
docker compose down
docker volume rm autocheckblazor_keycloak_data
docker compose up -d
```

## AuthService.EnsureLinkedAsync()

При першому вході:
1. Шукає UserLink по KeycloakSub
2. Якщо не знайдено:
   - Шукає StudentRecord по email (для pre-seeded студентів)
   - Якщо не знайдено → створює новий StudentRecord
   - Для нового студента → створює Submission (Locked) для всіх існуючих лаб
   - Зберігає UserLink
