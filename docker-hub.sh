#!/bin/bash

# Check if the password environment variable is set
if [[ -z "$DOCKERHUB_PW" ]]; then
    echo "Error: The DOCKERHUB_PW environment variable is not set."
    exit 1
fi

read -p "Enter DockerHub Username: " username
read -p "Enter DockerHub Email: " email

kubectl create secret docker-registry docker-hub --docker-username="$username" --docker-password="$DOCKERHUB_PW" --docker-email="$email"
