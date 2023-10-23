@echo off
setlocal

if "%1"=="" (
    echo Usage: Watch-K8sLogs.bat [APP_LABEL]
    exit /b 1
)

set APP_LABEL=%1

:loop
    for /f "usebackq" %%i in (`kubectl get pods --selector=app^=%APP_LABEL% -o jsonpath^="{.items[0].metadata.name}"`) do set "POD_NAME=%%i"
    if not "%POD_NAME%"=="" (
        echo Fetching logs for pod %POD_NAME%
        kubectl logs -f %POD_NAME% | cat
    ) else (
        echo No pod found with selector app=%APP_LABEL%. Retrying...
    )
    sleep 2
goto loop
