@echo off

:: Check if the password environment variable is set
if not defined DOCKERHUB_PW (
    echo Error: The DOCKERHUB_PW environment variable is not set.
    exit /b 1
)

set /p username=Enter DockerHub Username: 
set /p email=Enter DockerHub Email: 

kubectl create secret docker-registry docker-hub --docker-username=%username% --docker-password=%DOCKERHUB_PW% --docker-email=%email%
