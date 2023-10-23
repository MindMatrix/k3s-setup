#!/bin/bash
ansible-playbook playbooks/deploy.yaml
ansible-playbook node-affinity.yaml