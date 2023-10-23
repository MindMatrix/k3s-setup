#!/bin/bash

if [ -z "$1" ]; then
    echo "Usage: watch-k8s-logs.sh [APP_LABEL]"
    exit 1
fi

APP_LABEL=$1

while true; do
    POD_NAME=$(kubectl get pods --selector=app=$APP_LABEL -o jsonpath="{.items[0].metadata.name}")
    if [ ! -z "$POD_NAME" ]; then
        echo "Fetching logs for pod $POD_NAME"
        kubectl logs -f $POD_NAME | cat
    else
        echo "No pod found with selector app=$APP_LABEL. Retrying..."
    fi
    sleep 2
done