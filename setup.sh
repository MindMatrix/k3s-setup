#!/bin/bash
export K3S_TOKEN="$(openssl rand -base64 100 | tr -d '\n+/=' | cut -c -100)"
mkdir group_vars

ansible-playbook setup.yaml
