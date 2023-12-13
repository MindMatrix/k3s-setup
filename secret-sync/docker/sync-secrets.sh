#!/bin/bash

SOURCE_NAMESPACE="${SOURCE_NAMESPACE:-default}"
TARGET_NAMESPACE="${TARGET_NAMESPACE}"
SECRET_NAMES=(${SECRET_NAMES})

for SECRET_NAME in "${SECRET_NAMES[@]}"; do
  kubectl get secret "$SECRET_NAME" --namespace="$SOURCE_NAMESPACE" -o yaml |
  sed '/^\s*namespace:\s/d' |  # Remove namespace field
  sed '/^\s*creationTimestamp:\s/d' |  # Remove creationTimestamp
  sed '/^\s*resourceVersion:\s/d' |  # Remove resourceVersion
  sed '/^\s*selfLink:\s/d' |  # Remove selfLink
  sed '/^\s*uid:\s/d' |  # Remove uid
  kubectl apply --namespace="$TARGET_NAMESPACE" -f -
done
