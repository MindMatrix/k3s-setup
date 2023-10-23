#!/bin/bash
export PATH=$PATH:/usr/sbin

# Validate input parameters
if [[ -z $1 || -z $2 || -z $3 || -z $4 || -z $5 || -z $6 || -z $7 || -z $8 || -z $9 ]]; then
        echo "Usage: $0 <NAME> <ID> <IP> <GATEWAY> <MEMORY> <CPUS> <CORES> <HDD> <GITHUB_USER>"
        exit 1
fi

# Assign params to variables
VMNAME=$1
VMID=$2
VMIP=$3
VMGATEWAY=$4
VMMEMORY=$5
VMCPUS=$6
VMCORES=$7
VMHDD=$8
VMGITHUBUSER=$9

# Create VM
qm create $VMID --name="$VMNAME" --memory $VMMEMORY --net0 virtio,bridge=vmbr0 --agent 1 --sockets $VMCPUS --cores $VMCORES

# Wait for VM to be created
while [[ $(qm status $VMID) != *"status: stopped"* ]]; do
        sleep 2
done

# Enable cloud-init
qm set $VMID --ide2 local-lvm:cloudinit

# Set IP
qm set $VMID --ipconfig0 ip="$VMIP,gw=$VMGATEWAY"

# Set SSH Key
if [ ! -f $VMGITHUBUSER.keys ]; then
        curl -sL "https://github.com/$VMGITHUBUSER.keys" > $VMGITHUBUSER.keys
fi

qm set $VMID --ciuser "$VMGITHUBUSER" --sshkeys $VMGITHUBUSER.keys

# Set startup on boot
qm set $VMID --onboot 1

# Check if we already have the img
if [ ! -f ubuntu-22.04-minimal-cloudimg-amd64.img ]; then
        wget https://cloud-images.ubuntu.com/minimal/releases/jammy/release/ubuntu-22.04-minimal-cloudimg-amd64.img
fi

# Copy the image to a VMID.qcow2 so that we can resize it
cp ubuntu-22.04-minimal-cloudimg-amd64.img $VMID.qcow2

# Resize the image for the VM
qemu-img resize $VMID.qcow2 $VMHDD

# Import the HDD to the VM
qm importdisk $VMID $VMID.qcow2 local-lvm
rm $VMID.qcow2

# Add the disk to the VM
qm set $VMID --ide0 local-lvm:vm-$VMID-disk-0

# Move the scsi0 to the first boot device
qm set $VMID --boot c --bootdisk ide0

# Enable Proxmox console in UI
qm set $VMID --serial0 socket --vga serial0

# Enable Proxmox console in UI
qm start $VMID