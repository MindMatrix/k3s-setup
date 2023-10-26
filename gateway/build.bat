@echo off
set BUILD_SPACE=%~dp0
set BUILD_SPACE=%BUILD_SPACE:~0,-14%
set BUILD_OUTPUT=%BUILD_SPACE%\Builds
echo Workspace: %BUILD_SPACE%
echo Output:  %BUILD_OUTPUT%
IF NOT EXIST "%BUILD_OUTPUT%" mkdir "%BUILD_OUTPUT%"
echo %BUILD_SPACE%> "%BUILD_OUTPUT%\BUILD_SPACE.env"
echo %BUILD_OUTPUT%> "%BUILD_OUTPUT%\BUILD_OUTPUT.env"

set BUILD_START=%time%
powershell get-date -format "{yyyy}" > "%BUILD_OUTPUT%\BUILD_YEAR.env"
set /p BUILD_YEAR=<"%BUILD_OUTPUT%\BUILD_YEAR.env"
echo Year: %BUILD_YEAR%

REM @echo off
git rev-parse --abbrev-ref HEAD > "%BUILD_OUTPUT%\BUILD_BRANCH.env"
set /p BUILD_BRANCH=<"%BUILD_OUTPUT%\BUILD_BRANCH.env"
echo Branch:  %BUILD_BRANCH%
REM @echo off

powershell get-date -format "{yyyy.MM.dd.HHmm}" > "%BUILD_OUTPUT%\BUILD_NUMBER.env"
set /p BUILD_NUMBER=<"%BUILD_OUTPUT%\BUILD_NUMBER.env"
echo %BUILD_NUMBER%>"%BUILD_OUTPUT%\BUILD_NUMBER.env"
echo Version: %BUILD_NUMBER%

git rev-parse --verify HEAD > "%BUILD_OUTPUT%\BUILD_COMMIT.env"
set /p BUILD_COMMIT=<"%BUILD_OUTPUT%\BUILD_COMMIT.env"
echo %BUILD_COMMIT:~0,6%> "%BUILD_OUTPUT%\BUILD_SHORTCOMMIT.env"
echo Commit:  %BUILD_COMMIT%

REM docker build . -t mindmatrix/gateway:%BUILD_NUMBER%
dotnet publish src\gateway.csproj --os linux --arch x64 -c Release -p:ContainerImageTag=%BUILD_NUMBER%
if %errorlevel% neq 0 (
    echo Build failed. 
) else (
    docker push mindmatrix/gateway:%BUILD_NUMBER%
    kubectl set image deployment/gateway gateway=mindmatrix/gateway:%BUILD_NUMBER%

)
