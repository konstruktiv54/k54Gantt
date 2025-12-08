@echo off
setlocal EnableDelayedExpansion

echo ========================================
echo K54GANTT - OBFUSCATION SCRIPT
echo ========================================
echo.

:: Переходим в корень проекта
cd /d "%~dp0"

echo [1/5] Очистка старых сборок...
dotnet clean -c Release >nul 2>&1

echo.
echo [2/5] Сборка Release (x64)...
dotnet build -c Release -a x64 --self-contained false /p:Platform=x64 /p:DebugType=None /p:DebugSymbols=false
if errorlevel 1 (
    echo.
    echo *** ОШИБКА СБОРКИ! ***
    goto error
)

:: Путь к собранным файлам (актуален для .NET 8+ SDK-style проектов)
set "SOURCE_DIR=bin\x64\Release\net8.0-windows"
:: Если у тебя .NET 10 — просто замени на net10.0-windows
:: set "SOURCE_DIR=bin\x64\Release\net10.0-windows"

:: Папка с обфусцированными файлами
set "TARGET_DIR=bin\Obfuscated"

echo.
echo [3/5] Подготовка папки для обфусцированных файлов...
if exist "%TARGET_DIR%" rd /s /q "%TARGET_DIR%"
mkdir "%TARGET_DIR%"

echo.
echo [4/5] Запуск Obfuscar...
obfuscar.console.exe obfuscar.xml
if errorlevel 1 (
    echo.
    echo *** ОШИБКА OBFUSCAR! Проверь obfuscar.xml и логи выше ***
    goto error
)

:: Проверка, что основные сборки действительно обфусцировались
if not exist "%TARGET_DIR%\k54Gantt.exe" if not exist "%TARGET_DIR%\k54Gantt.dll" (
    echo.
    echo *** Не найдена обфусцированная сборка! Что-то пошло не так ***
    goto error
)

echo.
echo [5/5] Копирование всех НЕ обфусцированных файлов (runtimes, deps.json, wwwroot и т.д.)...
for /r "%SOURCE_DIR%" %%F in (*) do (
    set "filename=%%~nxF"
    set "skip=0"

    :: Не копируем то, что Obfuscar уже обработал (по имени файла)
    if /i "!filename!"=="k54Gantt.exe" set skip=1
    if /i "!filename!"=="k54Gantt.dll" set skip=1
    if /i "!filename!"=="Core.dll"      set skip=1
    if /i "!filename!"=="Wpf.dll"        set skip=1

    if !skip! EQU 0 (
        copy /Y "%%F" "%TARGET_DIR%\" >nul
    )
)

:: Дополнительно: гарантируем, что .exe тоже на месте (иногда он не в Module, но нужен для запуска)
if exist "%SOURCE_DIR%\k54Gantt.exe" (
    if not exist "%TARGET_DIR%\k54Gantt.exe" (
        copy /Y "%SOURCE_DIR%\k54Gantt.exe" "%TARGET_DIR%\k54Gantt.exe" >nul
    )
)

echo.
echo ========================================
echo ГОТОВО! Обфусцированная версия здесь:
echo %CD%\%TARGET_DIR%
echo.
echo Проверь в ILSpy / dnSpy:
echo     %TARGET_DIR%\k54Gantt.exe  (или .dll)
echo     %TARGET_DIR%\Core.dll
echo ========================================
echo.
echo Нажми любую клавишу для выхода...
pause >nul
exit /b 0

:error
echo.
echo ========================================
echo *** ОШИБКА! Обфускация НЕ выполнена ***
echo ========================================
pause
exit /b 1