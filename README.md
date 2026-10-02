## ⚙️ Подготовка Git Bash к работе с Docker

**Это ключевой шаг, без которого ничего не заработает.** Git Bash автоматически конвертирует Unix-пути (`/app`) в Windows-пути (`C:/Program Files/Git/app`), из-за чего Docker падает.

Откройте **Git Bash** и выполните один раз за сессию:

```bash
export MSYS_NO_PATHCONV=1
```

Проверьте, что Docker работает и в Linux-режиме:

```bash
docker info | grep -i "ostype"
# должно быть: OSType: linux
```

Если `OSType: windows` — переключите Docker Desktop в трей-иконке: **Switch to Linux containers**.

Smoke-тест:

```bash
docker run --rm hello-world
```

Должно напечатать «Hello from Docker!».

> 💡 Если `MSYS_NO_PATHCONV=1` не помогает (в старых версиях Git Bash), используйте вместо `-w /app` → `-w //app` (двойной слэш). Об этом ниже.

---

## Шаг 1. Создание структуры проекта

### 1.1. Перейдите в домашний каталог

```bash
cd ~
pwd
# должно быть: /c/Users/nikit  (или ваш аналог)
```

### 1.2. Создайте структуру

Выполните **одним блоком** (можно скопировать целиком из методички, но я привожу его ещё раз для полноты):

```bash
mkdir -p hello-dotnet/{.github/workflows,src,tests} && \
cd hello-dotnet && \

cat > src/HelloDotnet.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>hello-dotnet</AssemblyName>
    <RootNamespace>HelloDotnet</RootNamespace>
    <InvariantGlobalization>true</InvariantGlobalization>
    <Version>0.1.0</Version>
  </PropertyGroup>

</Project>
EOF

cat > src/Greeting.cs << 'EOF'
namespace HelloDotnet;

public static class Greeting
{
    public static string Greet(string name)
    {
        return $"Hello, {name}!";
    }

    public static int SumRange(int from, int to)
    {
        int sum = 0;
        for (int i = from; i <= to; i++)
        {
            sum += i;
        }
        return sum;
    }
}
EOF

cat > src/Program.cs << 'EOF'
using System.Reflection;
using System.Runtime.InteropServices;
using HelloDotnet;

// Версия читается из атрибута сборки, который задаётся в .csproj (<Version>)
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    .Split('+')[0] ?? "unknown";

Console.WriteLine($"hello-dotnet version {version}");
Console.WriteLine("Hello from C# in GitHub Actions! 🚀📦");
Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
Console.WriteLine($"Arch: {RuntimeInformation.OSArchitecture}");
Console.WriteLine(Greeting.Greet("GitHub"));
Console.WriteLine($"Sum 1..10 = {Greeting.SumRange(1, 10)}");

if (args.Length > 0)
{
    Console.WriteLine("Аргументы:");
    for (int i = 0; i < args.Length; i++)
    {
        Console.WriteLine($"  {i + 1}: {args[i]}");
    }
}

return 0;
EOF

cat > tests/HelloDotnet.Tests.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\src\HelloDotnet.csproj" />
  </ItemGroup>

</Project>
EOF

cat > tests/GreetingTests.cs << 'EOF'
using HelloDotnet;
using Xunit;

namespace HelloDotnet.Tests;

public class GreetingTests
{
    [Fact]
    public void Greet_ReturnsExpectedMessage()
    {
        Assert.Equal("Hello, .NET!", Greeting.Greet(".NET"));
        Assert.Equal("Hello, CI!", Greeting.Greet("CI"));
    }

    [Fact]
    public void SumRange_ReturnsCorrectSum()
    {
        Assert.Equal(55, Greeting.SumRange(1, 10));
        Assert.Equal(5050, Greeting.SumRange(1, 100));
    }
}
EOF

cat > .github/workflows/ci.yml << 'EOF'
name: .NET CI/CD

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]
  pull_request:

jobs:
  # ===== Job 1: CI — форматирование, сборка, тесты =====
  test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Cache NuGet
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore dependencies
        run: |
          dotnet restore src/HelloDotnet.csproj
          dotnet restore tests/HelloDotnet.Tests.csproj

      - name: Format check (src)
        run: dotnet format src/HelloDotnet.csproj --verify-no-changes --verbosity diagnostic

      - name: Format check (tests)
        run: dotnet format tests/HelloDotnet.Tests.csproj --verify-no-changes --verbosity diagnostic

      - name: Build
        run: dotnet build tests/HelloDotnet.Tests.csproj --configuration Release --no-restore

      - name: Run tests
        run: dotnet test tests/HelloDotnet.Tests.csproj --configuration Release --no-build --verbosity normal

  # ===== Job 2: CD — публикация бинарников по тегу =====
  release:
    needs: test
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: ubuntu-latest

    permissions:
      contents: write

    strategy:
      matrix:
        include:
          - rid: linux-x64
            artifact: hello-dotnet-linux-x64
          - rid: linux-arm64
            artifact: hello-dotnet-linux-arm64
          - rid: win-x64
            artifact: hello-dotnet-windows-x64.exe
          - rid: osx-x64
            artifact: hello-dotnet-macos-x64
          - rid: osx-arm64
            artifact: hello-dotnet-macos-arm64

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Cache NuGet
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Publish for ${{ matrix.rid }}
        run: |
          dotnet publish src/HelloDotnet.csproj \
            --configuration Release \
            --runtime ${{ matrix.rid }} \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:IncludeNativeLibrariesForSelfExtract=true \
            --output dist/${{ matrix.rid }}

      - name: Rename binary
        run: |
          if [[ "${{ matrix.rid }}" == win-* ]]; then
            mv dist/${{ matrix.rid }}/hello-dotnet.exe dist/${{ matrix.artifact }}
          else
            mv dist/${{ matrix.rid }}/hello-dotnet dist/${{ matrix.artifact }}
          fi

      - name: Upload to Release
        uses: softprops/action-gh-release@v2
        with:
          files: dist/${{ matrix.artifact }}
          generate_release_notes: true
EOF

cat > .gitignore << 'EOF'
bin/
obj/
*.user
.vs/
.idea/
.vscode/
*.suo
.env
dist/
EOF

echo "✅ Структура создана:"
find . -type f | sort
```

### 1.3. Проверка

```bash
pwd
# /c/Users/nikit/hello-dotnet

ls -la
# .github  src  tests  .gitignore

find . -type f | sort
# ./.github/workflows/ci.yml
# ./.gitignore
# ./src/Greeting.cs
# ./src/HelloDotnet.csproj
# ./src/Program.cs
# ./tests/GreetingTests.cs
# ./tests/HelloDotnet.Tests.csproj
```

Если видите 7 файлов — структура создана корректно.

> ⚠️ Если `cat << 'EOF'` не сработал и файлы пустые/отсутствуют — проверьте шелл: `echo $SHELL` должно быть `/usr/bin/bash` или подобное. В **PowerShell** этот синтаксис не работает.

---

## Шаг 2. Тесты в Docker (.NET на хосте не нужен)

### 2.1. Убедитесь, что вы в папке проекта и `MSYS_NO_PATHCONV=1` включён

```bash
cd ~/hello-dotnet
pwd
export MSYS_NO_PATHCONV=1
```

### 2.2. Запустите тесты

```bash
docker run --rm \
  -u "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/tmp/nuget \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -v "$(pwd)":/app \
  -w /app \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/HelloDotnet.Tests.csproj
```

### 2.3. Разбор флагов

| Флаг | Зачем |
|---|---|
| `--rm` | Удалить контейнер после выхода |
| `-u "$(id -u):$(id -g)"` | Запуск от вашего UID/GID — файлы `bin/`, `obj/` не остаются `root`-овыми |
| `-e HOME=/tmp` | Перенаправляем HOME внутрь контейнера |
| `-e DOTNET_CLI_HOME=/tmp` | Служебные файлы .NET CLI — внутрь |
| `-e NUGET_PACKAGES=/tmp/nuget` | Кэш NuGet — внутрь (не засоряем проект) |
| `-e DOTNET_CLI_TELEMETRY_OPTOUT=1` | Отключаем телеметрию |
| `-e DOTNET_NOLOGO=1` | Убираем баннер .NET |
| `-v "$(pwd)":/app` | Монтируем текущую папку в `/app` |
| `-w /app` | Рабочий каталог внутри контейнера |

> 💡 **Если `-w /app` всё равно конвертируется в `C:/Program Files/Git/app`** — замените на `-w //app` (двойной слэш отключает конвертацию для конкретного аргумента). Тогда `export MSYS_NO_PATHCONV` не нужен.

### 2.4. Ожидаемый вывод

```
Determining projects to restore...
Restored /app/tests/HelloDotnet.Tests.csproj
...
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2
```

<img width="1745" height="786" alt="image" src="https://github.com/user-attachments/assets/0f691f23-dcf4-48bf-a25f-798d0988fdc7" />

Если увидели `Passed!` — идём дальше.

---

## Шаг 3. Локальная сборка бинарника через `dotnet publish` в Docker

### 3.1. Запустите сборку под Linux x64

```bash
cd ~/hello-dotnet
export MSYS_NO_PATHCONV=1

docker run --rm \
  -u "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/tmp/nuget \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -v "$(pwd)":/app \
  -w /app \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet publish src/HelloDotnet.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o dist/linux-x64
```

### 3.2. Разбор флагов `dotnet publish`

| Флаг | Что делает |
|---|---|
| `-c Release` | Конфигурация Release (оптимизации) |
| `-r linux-x64` | Runtime Identifier — целевая платформа |
| `--self-contained true` | .NET runtime включается в бинарник |
| `-p:PublishSingleFile=true` | Один файл вместо десятков DLL |
| `-p:IncludeNativeLibrariesForSelfExtract=true` | Нативные библиотеки упаковываются внутрь |
| `-o dist/linux-x64` | Каталог вывода |

### 3.3. Проверка

```bash
ls -lh dist/linux-x64/
# -rwxr-xr-x ... 70M ... hello-dotnet
```

~70 MB — нормально: внутри весь .NET runtime.

### 3.4. Запуск в чистом Debian (без .NET)

```bash
docker run --rm \
  -v "$(pwd)/dist/linux-x64":/dist \
  debian:stable-slim \
  /dist/hello-dotnet
```

Ожидаемый вывод:

```
hello-dotnet version 0.1.0
Hello from C# in GitHub Actions! 🚀📦
OS: Debian GNU/Linux 12 (bookworm)
Arch: X64
Hello, GitHub!
Sum 1..10 = 55
```
<img width="1359" height="256" alt="image" src="https://github.com/user-attachments/assets/bd0765d4-88e9-4bc9-920e-df3700fb960b" />

Это доказывает, что бинарник **self-contained** — .NET на целевой системе не нужен.

> 🔑 **Ключевое отличие от Python:** один runner собирает под все платформы через `-r <RID>` — как `GOOS=windows go build` в Go. Не нужны отдельные macOS/Windows-раннеры.

---

## Шаг 4. Создание пустого репозитория на GitHub

1. Откройте https://github.com/new
2. **Repository name:** `hello-dotnet`
3. **Visibility:** Public (или Private — тогда проверьте права Actions в Settings → Actions → General)
4. ⚠️ **НЕ ставьте галочки** на:
   - Add a README file
   - Add .gitignore
   - Choose a license

   Иначе `git push` будет отклонён (несовпадающие истории).
5. Нажмите **Create repository**.

---

## Шаг 5. Пуш проекта в GitHub

### 5.1. Вернитесь в папку проекта

```bash
cd ~/hello-dotnet
pwd
# /c/Users/nikit/hello-dotnet
```

### 5.2. Инициализация и первый коммит

```bash
git init
git add .
git commit -m "Initial commit: .NET CLI with CI/CD to Releases"
git branch -M main
```
<img width="1779" height="394" alt="image" src="https://github.com/user-attachments/assets/bc31a380-a9df-4ec8-8765-6911a747d3bd" />

### 5.3. Настройка remote

```bash
read -p "Введите ваш GitHub username: " GITHUB_USER
git remote add origin "https://github.com/${GITHUB_USER}/hello-dotnet.git"
git remote -v
# origin  https://github.com/<username>/hello-dotnet.git (fetch)
# origin  https://github.com/<username>/hello-dotnet.git (push)
```
<img width="1802" height="188" alt="image" src="https://github.com/user-attachments/assets/f7084fe0-9076-49c7-8ab7-6b39572bd60e" />

### 5.4. Push

```bash
git push -u origin main
```

При первом push Git попросит логин/пароль. **GitHub не принимает обычный пароль** — нужен **Personal Access Token**:

1. GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic) → **Generate new token (classic)**
2. Scope: **`repo`** (полный доступ к репозиториям)
3. Скопируйте токен
4. При запросе пароля вставьте токен

> 💡 Чтобы не вводить каждый раз — настройте credential helper:
> ```bash
> git config --global credential.helper manager
> ```
> Тогда Git сохранит токен в Windows Credential Manager.

---

## Шаг 6. Первый запуск CI (без релиза)

### 6.1. Откройте Actions

```
https://github.com/<ВАШ-USERNAME>/hello-dotnet/actions
```

Увидите workflow **.NET CI/CD** в статусе *in progress* → через ~2–3 минуты **зелёная галочка** ✅.

### 6.2. Что произошло

- ✅ **Job `test`** — прошёл:
  - `actions/checkout@v4` — клонирование репозитория
  - `actions/setup-dotnet@v4` — установка .NET 8
  - Кэш NuGet
  - `dotnet restore` для обоих проектов
  - `dotnet format --verify-no-changes` (проверка форматирования)
  - `dotnet build`
  - `dotnet test`
- ⏭️ **Job `release`** — **пропущен**, потому что `if: startsWith(github.ref, 'refs/tags/v')` не выполнено (push в ветку, а не тег)

Это нормально. Релиз создаётся только при push тега `v*`.

### 6.3. Если job `test` упал на `dotnet format`

Значит, код не отформатирован. Исправьте локально (см. Шаг 9.3) и запушьте снова.

---

## Шаг 7. Создание релиза v0.1.0

### 7.1. Убедитесь, что `main` стабилен

Все тесты зелёные на GitHub Actions.

### 7.2. Создайте тег

```bash
cd ~/hello-dotnet

git tag v0.1.0
git push origin v0.1.0
```
### 7.3. Что произойдёт

1. GitHub видит push тега `v0.1.0`
2. Workflow **.NET CI/CD** запускается снова, теперь срабатывает `release`
3. **Job `test`** прогонит тесты (~1 мин)
4. **Job `release`** запустит 5 параллельных сборок (~3–4 мин)
5. **`softprops/action-gh-release@v2`** создаст Release и прикрепит файлы

### 7.4. Проверка

```
https://github.com/<ВАШ-USERNAME>/hello-dotnet/releases
```

Должен быть релиз **v0.1.0** с 5 файлами:

| Файл | Платформа |
|---|---|
| `hello-dotnet-linux-x64` | Linux Intel/AMD |
| `hello-dotnet-linux-arm64` | Linux ARM |
| `hello-dotnet-windows-x64.exe` | Windows Intel/AMD |
| `hello-dotnet-macos-x64` | macOS Intel |
| `hello-dotnet-macos-arm64` | macOS Apple Silicon |

Плюс автоматически сгенерированные release notes.

<img width="1733" height="900" alt="image" src="https://github.com/user-attachments/assets/dbd08f59-03b4-4c94-8783-74717bcefa56" />

---

## Шаг 8. Скачивание и запуск бинарника

### 8.1. Linux x64 (Git Bash / WSL)

```bash
cd ~
read -p "Введите ваш GitHub username: " GITHUB_USER
wget "https://github.com/${GITHUB_USER}/hello-dotnet/releases/download/v0.1.0/hello-dotnet-linux-x64" -O hello-dotnet
chmod +x hello-dotnet
./hello-dotnet
```

### 8.2. macOS Apple Silicon (Git Bash на macOS)

```bash
cd ~
read -p "Введите ваш GitHub username: " GITHUB_USER
curl -L "https://github.com/${GITHUB_USER}/hello-dotnet/releases/download/v0.1.0/hello-dotnet-macos-arm64" -o hello-dotnet
chmod +x hello-dotnet
./hello-dotnet
```

> ⚠️ На macOS при первом запуске может появиться предупреждение Gatekeeper. Обход:
> - Системные настройки → Приватность и безопасность → **Всё равно открыть**
> - Или в терминале: `xattr -d com.apple.quarantine ./hello-dotnet`

### 8.3. Windows x64 (Git Bash + WSL — рекомендуемый путь)

**Вариант через WSL:** зайдите в WSL (`wsl` в Git Bash или отдельное окно), оттуда:

```bash
cd ~
read -p "Введите ваш GitHub username: " GITHUB_USER
wget "https://github.com/${GITHUB_USER}/hello-dotnet/releases/download/v0.1.0/hello-dotnet-linux-x64" -O hello-dotnet
chmod +x hello-dotnet
./hello-dotnet
```

**Вариант через Git Bash напрямую (Windows .exe):**

```bash
cd ~
read -p "Введите ваш GitHub username: " GITHUB_USER
curl -L "https://github.com/${GITHUB_USER}/hello-dotnet/releases/download/v0.1.0/hello-dotnet-windows-x64.exe" -o hello-dotnet.exe
./hello-dotnet.exe
```

Git Bash умеет запускать `.exe` — просто `./hello-dotnet.exe`.

### 8.4. Ожидаемый вывод

```
hello-dotnet version 0.1.0
Hello from C# in GitHub Actions! 🚀📦
OS: Linux 6.5.0-27-generic
Arch: X64
Hello, GitHub!
Sum 1..10 = 55
```
<img width="1687" height="293" alt="image" src="https://github.com/user-attachments/assets/8a308d31-534b-4c68-826c-9d80db4f0d26" />

`OS` и `Arch` зависят от платформы запуска. `X64` — Intel/AMD, `Arm64` — Apple Silicon / ARM-серверы.

---

## Шаг 9. Обновление релиза до v0.2.0

### 9.1. Семантическое версионирование

Формат `MAJOR.MINOR.PATCH`:

| Что изменили | Какую версию | Пример |
|---|---|---|
| Исправили баг | patch | `0.1.0` → `0.1.1` |
| Добавили функцию | minor | `0.1.0` → `0.2.0` |
| Сломали совместимость | major | `0.1.0` → `1.0.0` |

Наш выбор — **`0.2.0`**.

### 9.2. Обновите версию в одном файле

Откройте `src/HelloDotnet.csproj` в VS Code:

```bash
cd ~/hello-dotnet
code src/HelloDotnet.csproj
```

Измените:

```xml
<PropertyGroup>
  ...
  <Version>0.2.0</Version>   <!-- ← было 0.1.0 -->
</PropertyGroup>
```

**Больше нигде менять не нужно** — `Program.cs` читает версию из атрибута сборки автоматически.

✅ **Преимущество .NET перед Python:** версия в одном файле. В Python — в двух (`pyproject.toml` + `__init__.py`).

### 9.3. (Опционально) Измените `Program.cs`

Например, добавьте строку:

```bash
code src/Program.cs
```

```csharp
Console.WriteLine("🎉 New in v0.2.0!");
```

### 9.4. Проверьте форматирование локально

CI запускает `dotnet format --verify-no-changes` и упадёт, если код не отформатирован. Проверьте заранее:

```bash
cd ~/hello-dotnet
export MSYS_NO_PATHCONV=1

docker run --rm \
  -u "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/tmp/nuget \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -v "$(pwd)":/app \
  -w /app \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  sh -c "dotnet format src/HelloDotnet.csproj && dotnet format tests/HelloDotnet.Tests.csproj"
```

**Важно:** здесь **без** `--verify-no-changes` — команда **автоматически исправит** форматирование. После этого нужно закоммитить изменения, иначе CI всё равно упадёт.

### 9.5. Проверьте тесты

```bash
docker run --rm \
  -u "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/tmp/nuget \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -v "$(pwd)":/app \
  -w /app \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/HelloDotnet.Tests.csproj
```

Ожидаемо:

```
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2
```

<img width="1796" height="247" alt="image" src="https://github.com/user-attachments/assets/691d3224-5b5a-4815-8835-9fa252dd91aa" />

### 9.6. Закоммитьте и запушьте

```bash
git add .
git commit -m "feat: bump version to 0.2.0"
git push origin main
```

Что произойдёт:
- ✅ **Job `test`** пройдёт
- ⏭️ **Job `release`** пропущен (push в ветку, не тег)

### 9.7. Создайте новый тег

```bash
git tag v0.2.0
git push origin v0.2.0
```

Что произойдёт:
- ✅ **`test`** — снова прогонит тесты
- ✅ **`release`** — соберёт 5 бинарников параллельно
- ✅ Создастся **Release v0.2.0**

### 9.8. Проверьте результат

```
https://github.com/<ВАШ-USERNAME>/hello-dotnet/releases
```

Должно быть **два релиза**:
- `v0.1.0` — старый (не тронут)
- `v0.2.0` — новый (с изменениями)

### 9.9. Скачайте и запустите новый бинарник

```bash
cd ~
read -p "Введите ваш GitHub username: " GITHUB_USER
URL="https://github.com/${GITHUB_USER}/hello-dotnet/releases/download/v0.2.0/hello-dotnet-linux-x64"

wget "$URL" -O hello-dotnet
chmod +x hello-dotnet
./hello-dotnet
```

Ожидаемый вывод:

```
hello-dotnet version 0.2.0
Hello from C# in GitHub Actions! 🚀📦
OS: Linux 6.5.0-27-generic
Arch: X64
Hello, GitHub!
Sum 1..10 = 55
```

---

## 🛠 Если ошиблись с тегом

Например, создали `v0.2.0`, но забыли обновить `<Version>` в `.csproj`.

### Удалить тег локально
```bash
git tag -d v0.2.0
```

### Удалить тег на GitHub
```bash
git push origin :refs/tags/v0.2.0
```

### Удалить Release на GitHub
Откройте `https://github.com/<ВАШ-USERNAME>/hello-dotnet/releases`, у нужного релиза → **Delete**.

### Исправить и повторить
```bash
cd ~/hello-dotnet
code src/HelloDotnet.csproj   # обновить <Version>
git add .
git commit -m "fix: correct version in csproj"
git push origin main

git tag v0.2.0
git push origin v0.2.0
```

> ⚠️ **Релизы в GitHub неизменяемы.** Нельзя перезаписать файлы внутри `v0.1.0`. Для нового кода — новая версия.

---
