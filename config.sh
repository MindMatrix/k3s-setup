#!/bin/bash
ansible-playbook playbooks/apt.yml
ansible-playbook playbooks/qemu-guest-agent.yml
ansible-playbook playbooks/timezone.yml
ansible-playbook playbooks/longhorn.yml
ansible-playbook playbooks/reboot.yml