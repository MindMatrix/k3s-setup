#!/bin/bash
# Validate input parameters
if [[ -z $1 ]]; then
        echo "Usage: $0 <GITHUB_USERID>"
        exit 1
fi

GITHUB_USER=$1

# ubuntu doesn't have the latest ansible, need to use ppa
sudo apt update
sudo apt install software-properties-common apt-transport-https ca-certificates curl -y
sudo add-apt-repository --yes --update ppa:ansible/ansible
curl -fsSL https://pkgs.k8s.io/core:/stable:/v1.28/deb/Release.key | sudo gpg --dearmor -o /etc/apt/keyrings/kubernetes-apt-keyring.gpg
echo 'deb [signed-by=/etc/apt/keyrings/kubernetes-apt-keyring.gpg] https://pkgs.k8s.io/core:/stable:/v1.28/deb/ /' | sudo tee /etc/apt/sources.list.d/kubernetes.list
curl https://baltocdn.com/helm/signing.asc | gpg --dearmor | sudo tee /usr/share/keyrings/helm.gpg > /dev/null
sudo apt-get install apt-transport-https --yes
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/helm.gpg] https://baltocdn.com/helm/stable/debian/ all main" | sudo tee /etc/apt/sources.list.d/helm-stable-debian.list
sudo apt-get update
sudo apt install ansible git vim python3-netaddr kubectl helm -y

# we need to generate a key for github auth on the server cluster
# check out the 2 repos to configure VM + k3s

git clone https://github.com/MindMatrix/k3s-setup
git clone https://github.com/techno-tim/k3s-ansible
chmod +x k3s-setup/*.sh
ansible-galaxy collection install -r ./k3s-ansible/collections/requirements.yml
ssh-keygen -t ed25519 -C "$GITHUB_USER" -f ~/.ssh/id_ed25519 -N ""

echo ""
echo ""
echo You need to import your private key in to github under your user account and ssh keys
cat .ssh/id_ed25519.pub
echo You also need to call "\"ssh-copy-id <GITHUB_USER>@proxmox_ip\"" for each proxmox server