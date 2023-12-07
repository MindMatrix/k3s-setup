## Undocumented Changes ##
1. Kubeseal is now used to protect secrets from being read at the `kubectl get secrets` level and will be decrypted only when used inside of a pod and at pod init - copied to `C:\ProgramData\chocolatey` for easy use
2. Helmify is used to convert a k8s yaml file collection to helm charts - copied to `C:\ProgramData\chocolatey` for easy use


**NOTE**: Need to document registry-cache, should be setup before longhorn

# Table of Contents
0. [Intro](#intro)
1. [PROXMOX](#proxmox)  
2. [Ansible](#ansible)
3. [Kubernetes](#kurbenetes)
4. [Longhorn FileSystem](#longhorn)
5. [Docker Hub](#dockerhub)
6. [Traefik](#traefik)
7. [Cert Manager](#certmanager)
8. [Forward Auth](#forwardauth)
9. [Deploy Kubernetes Dashboard](#kubernetesdashboard)
10. [Deploy Traefik Dashboard](#traefikdashboard)
11. [Finalize](#finalize)

## 0. Intro <a id="intro"></a>

This article assumes you have a basic understanding of kubernetes and proxmox. Several of the steps assume that you know to switch between proxmox and ansible to run specific commands. The majority of all configuration will be saved and executed from the created ansible server.

Additionally there will be several places you need to fill in the blank 
- `<GITHUB_USER>` should be a valid github userid as it will download your public keys for server access.
- `<VERIFIED_GITHUB_EMAIL>` should be a verified github email address linked the the same `GITHUB_USER`
- `<GATEWAY>` should be the gateway of your network
- `<IP_CDIR>` should be an IP in the format of `xxx.yyy.zzz.www/CDIR`

## 1. PROXMOX <a id="proxmox"></a>
This section explains the requirements and setup steps for PROXMOX which is a open source VM that has ssh support and extensive use of `command line tools` to mage VMs without a UI.

1. Create X machines with the latest PROXMOX installed on baremetal.
2. During the install it will ask for a domain, DO NOT use a real domain as this will break the internal DNS lookup of all the kubernetes pods. Instead use something like `<domain>.internal`.
3. Access PROXMOX on `<ip>:8006` and log in with the `root` password you created on install.
4. Under the UI, go to each server and make sure you have a valid DNS server with a backup, for example: `8.8.8.8` and `4.2.2.1` is a good starting point.
5. Click `datacenter` in the left panel in the UI and click create cluster.
6. Once the cluster is created you will access the PROXMOX UI on the other `<ip>:8006` one at a time and go to the data center of that server and then click `Join Cluster` and paste in the `Join Information` from the server the created the cluster.   
**NOTE**: Wait for each server to fully join as it will cause failures to have more than one joining at a time.
7. Its suggested to do an `apt update && apt install vim -y` as the version of VIM shipped on PROXMOX is clunky and produces errors with large text.
8. On each proxmox server you should create a user account and later use `scp-copy-id <user>@<proxip>` for each proxmox server from the ansible machine after you run the init command on ansible.  
**Note:** This password you pick for this user will be the password you must supply when using `ansible-playbok --ask-become-password` to use `sudo`.  
```shell
read -p "Enter GitHub username: " GITHUB_USER
adduser $GITHUB_USER
usermod -aG sudo $GITHUB_USER
apt update && apt install sudo -y
```
## 2. Ansible <a id="ansible"></a>
Ansible is a tool that utilizes a concept called `playbooks`. These playbooks describe a series of actions to perform or maintain on a list of servers. For example you could have it call `apt update && apt upgrade` to upgrade all hosts or you could have it script the setup of `timesyncd` and auto load the NTP from an air gapped time server. Ansible is also used to execute all the VM creation scripts across the PROXMOX machines and also create and maintain K3S (kubernetes).

1. Here is the suggested script to create the default ansible for creating the rest of the configuration.  
**NOTE:** The VM takes a minute or 2 to initialize with all the cloud-init settings the first time.
```shell
curl -sfL https://raw.githubusercontent.com/MindMatrix/k3s-setup/main/create-vm.sh | bash -s -- "ansible-<IP_SUFFIX>" <IP_SUFFIX> "<IP>/24" "<GATEWAY>" 4096 2 2 16G "<GITHUB_USER>"
```
Note: IP suffix helps for organizing and finding nodes. If the IP is `10.100.222.205` then you should set the `IP_SUFFIX` to `205` so that the `VM ID` is set to `205`.

2. Setup the ansible server configuration with an init script.
```shell
curl -sfL https://raw.githubusercontent.com/MindMatrix/k3s-setup/main/init-ansible.sh | bash -s -- "<GITHUB_USER>"
```

## 3. Kubernetes <a id="kurbenetes"></a>
1. After the init script is created it will also create a private key, the public portion of this key from `.ssh/id_ed25519.pub` should be import in to your github user account and this account should match the username you used through out.  

2. **NOTE: Your MetalLB first IP should be the `PUBLIC` to `INTERNAL` ip, if you public ip is `64.128.200.215` and points internally to `10.100.128.215` your first IP in the metal lb range should be `10.100.128.215` and should also be assigned to the ingress controller step.**  
Modify the `cluster.yaml` file in `k3s-setup` with the correct settings. You should look up the latest versions of `k3s`, `metallb` and `kube_vip`. You need to provide a list of IPs for `metallb` so it can create public services accessible to the network.  
**Note:** The `ansible_user` should match your github user.  
**Note:** Please make sure to have an `odd` number of `master` nodes to have proper `etcd` qurom.

3. Run the following to build the `hosts.ini` for `ansible` from the `k3s-setup` folder. This will also generate a `k3s_token`.  
**NOTE: COPY THE IDS, IMPORT KEYS IN TO GITHUB, MAKE SURE PROXMOX USER WAS CREATED, EDIT THE CLUSTER.YAML**
```shell
./setup.sh
```
4. Run the following command to create the VMs across the proxmox cluster. This can take awhile, please make sure your public key has already been added to github at this point.
```shell
./vm.sh
```
5. Run the following to update to the latest, install qemu-giest-agent and sync time clocks to expedient.  
**NOTE: The last VM will take 1-2 minutes to initialize from the previous command. Its best to check ssh access prior to running this command and moving on!**
```shell
./config.sh
```
6. Run the following to deploy k3s to all the VMs created and to create a `etcd` master cluster.
```shell
./deploy.sh
```
7. Copy the `kube config` from one of the `k3s master` nodes to the ansible machine so that the remaning steps can be carried out on the ansible machine instead of on a local machine.
```shell
mkdir ~/.kube
scp 10.100.126.204:~/.kube/config ~/.kube/config
```

## 4. Docker Hub <a id="dockerhub"></a>
To allow the repository to pull private images from our mindmatrix docker hub repository the following needs to be configured.

1. You can run the docker hub script and fill in the questions it asks you. You will need an access token from docker and will need to set it as an environment variable.
```shell
./docker-hub.sh
```
## 5. Traefik <a id="traefik"></a>
This sits inside of kubernetes with a public IP address both from the internet and the network. It will replace our nginx load balancer and handle all incoming external requests to properly route them where they need to go. It will also apply middleware like `forward-auth` to protect internal resources instead of each application needing to build it in.

1. You'll need to have `kubectl` and `helm` installed on a machine. You will need to copy the `~/.kube/config` file from one of the `k3s-master` nodes to your machine in to the same path. If you already have something like docker installed and have more than one kubectl endpoint you manage, you will need to edit your local file and manually merge the `kube config` to yours. You can use `kubectx` to switch between the contexts.
2. Use `kubectx` to switch to the context you wish to continue setting up.
3. Add and update repo
```shell
helm repo add traefik https://helm.traefik.io/traefik
helm repo update
``` 
4. Install traefik with the custom values, **NOTE**: Edit `values.yaml` first!  
**NOTE:** To make a change to values.yaml you need to run `helm upgrade traefik traefik/traefik -n traefik -f traefik/values.yaml`.
```
helm install --namespace=traefik traefik traefik/traefik --values=traefik/values.yaml --create-namespace
```
5. Verify that traefik got an ip under `EXTERANL-IP`
```shell
kubectl get svc --all-namespaces -o wide
```
Example
```shell
NAMESPACE        NAME              TYPE           CLUSTER-IP      EXTERNAL-IP     PORT(S)                      AGE   SELECTOR
default          kubernetes        ClusterIP      10.43.0.1       <none>          443/TCP                      16h   <none>
kube-system      kube-dns          ClusterIP      10.43.0.10      <none>          53/UDP,53/TCP,9153/TCP       16h   k8s-app=kube-dns
kube-system      metrics-server    ClusterIP      10.43.182.24    <none>          443/TCP                      16h   k8s-app=metrics-server
metallb-system   webhook-service   ClusterIP      10.43.205.142   <none>          443/TCP                      16h   component=controller
traefik          traefik           LoadBalancer   10.43.156.161   192.168.30.80   80:30358/TCP,443:31265/TCP   22s   app.kubernetes.io/instance=traefik,app.kubernetes.io/name=traefik
```
6. Apply middleware
```shell
kubectl apply -f traefik/default-headers.yaml
```
## 6. Cert Manager <a id="certmanager"></a>
This deployment sits inside kubernetes and waits for certificate requests made inside kubernetes. It will handle communication with lets encrypt and digital ocean to auto create and update SSL certs for traefik. If you can't use lets encrypt and dns challenges, you will need to update the CRT and KEY as a secret to kubernetes instead and manually manage it.

1. Add the heml info
```shell
helm repo add jetstack https://charts.jetstack.io
helm repo update
```
2. Setup cert manager
```shell
kubectl create namespace cert-manager
```
3. Install cert managers CRDs
```shell
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.1/cert-manager.crds.yaml
```
4. Install cert manager.  
**NOTE:** The initial config uses a single replica for cert manager to make it easier to test stagin, once done you will need to set the replicas to 3 for HA and run `helm upgrade cert-manager jetstack/cert-manager -n cert-manager -f cert-manager/values.yaml`
```shell
helm install cert-manager jetstack/cert-manager --namespace cert-manager --values=cert-manager/values.yaml --version v1.13.1
```
5. Set the digital ocean api key.  
**NOTE:** On premise can not use our digital ocean accounts and must have their own digital ocean account with their own API keys. There are other servers too like cloudflare and godaddy.  
**NOTE:** Update the file to have a proper access token!
```shell
kubectl apply -f cert-manager/issuers
```
6. Create the issuer for staging.  
**NOTE:** Its critical you test this with staging first as lets encrypt production is rate limited and their rate limiting once **`WEEKLY`**!!!
```shell
kubectl apply -f cert-manager/certificates/staging/gladeos.dev.yaml
```
7. To test that staging certificate you'll want to set proper hosts in the `cert-manager/nginx` folder and then run  
**NOTE:** This can take several minutes to complete, if you set the replica to 1, you can do a `kubectl get pods -n cert-manager` and then grab the name of the pod running and watch the logs with `kubectl logs -n cert-manager -f <podname>`. You should eventually see that there are no more jobs pending and can test it.  
**NOTE:** The default `nginx/ingress.yaml` points to a staging lets encrypt, so you will still get an invalid cert, but you need to view the cert and make sure it says staging lets encrypt and not `default traefik cert` as this means the cert isn't detected. Once this works, you can switch the `ingress.yaml` to use the production cert after you run `kubectl apply -f cert-manager/certificates/production/gladeos-com.yaml` (do not run this until you tested staging!!!!)  
```shell
kubectl apply -f cert-manager/nginx
```
8. Test the url, once it works, you need to apply production (after verifying the cert says STAGING and not default trafik cert).  
**NOTE:** Update `cert-manager/nginx/ingress.yaml` to put to the new cert aka: `gladeos-dev-staging-tls` to `gladeos-dev-tls`
```shell
kubectl apply -f cert-manager/certificates/production/gladeos.dev.yaml
```

## 7. Forward Auth & IP White list<a id="forwardauth"></a>
This middleware will be loaded in to kubernetes and will be used to protect all internal endpoints for devops and dashboards needed to maintain the state of the cluster. It will utilize out oAuth + Teams to provide access to specific internal tools.  
**NOTE:** dockerhub step has to be completed before this step or kubernetes will not have access to the private images on docker hub.
1. You need to modify `deployment.yaml`, `ingress.yaml` and `secrets.yaml` to correct values, like domain, github oauth settings and so on. You also need to go to docker hub and get the latest release version of forward-auth and update the `deployment.yaml`
```shell
kubectl apply -f forward-auth/k8s
```
2. Repeat the steps for IP White Listing  
**NOTE:** You should check the latest version of `ip-whitelist` and update the `cron.yaml`. You also need to update the `configmap.yaml` with the list of domains and ips to allow access, this job will refresh the data every 15 mins in the `ip-whitelist-middleware.yaml`.
```shell
kubectl apply -f ip-whitelist/k8s
```
3. IP White Listing is a cronjob in kubernetes that will rebuild the `ip-whitelist-middleware.yaml` every 15 minutes based on the data from `configmap.yaml`, so to add allowed IPs to the list, you edit the configmap and apply it with `kubectl apply -f 'ip-whitelist/configmap.yaml'`.  
You should check back later to see if the job completed successfully by doing `kubectl get pods` and look for the job and determine its status. If it failed, you can always do `kubectl logs <pod_name>` and get the logs.  
**NOTE:** You can manually trigger the job with the following, you should also delete the pod once done and verified.
```shell
kubectl create job --from=cronjob/update-ip-whitelist update-ip-whitelist-manual-run
```
Get the pod name and verify the logs `kubectl get pods -w` (this will watch for pod changes, you want to wait until the job says completed or failed)
```shell
kubectl logs <pod_name>
```
Verify the log data, aka
```shell
INFO:root:Resovled 'example.com' to '93.184.216.34/32'
INFO:root:Appending '10.100.126.0/24'
INFO:root:Appending '74.98.193.88/32'
```
Delete the pod and job
```shell
kubectl delete job update-ip-whitelist-manual-run
```
4. You should now verify that the white listing of ips is working by mdoifying the `cert-manager/nginx/ingress.yaml` and add `ip-whitelist` to the middleware section and verify you still have access to url after applying the changes.

## 8. Deploy Kubernetes Dashboard <a id="kubernetesdashboard"></a>
To successfully deploy the kubernetes dashboard to the cluster and secure it you will need to create a `service account` with correct `RBAC` permissions and you will need to have a properly deployed `forward-auth` which will protect the dashboard via `github oauth` and will also forward the `ID Token` upstream to auto auth in to the dashboard.

1. Deploy kubernetes dashboard, should check for the latest version prior to doing this.
```shell
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml
```
2. Run the following (update ingress)
```shell
kubectl apply -f kubernetes-dashboard
```

3. Get the access token to add as a secret to `forward-auth`.  
**NOTE:** Default time for this token is 10 minutes. You will need to use this every time you need to access the dashboard.
```shell
kubectl create token dashboard-adminuser -n kubernetes-dashboard
```
## 9. Deploy Traefik Dashboard <a id="traefikdashboard"></a>

1. Apply the ingress route to enable traefik.  
**NOTE: ** Edit the ingress.yaml file first.
```shell
kubectl apply -f traefik-dashboard
```
## 10. Longhorn FileSystem <a id="longhorn"></a>
Longhorn will use the storage space of the k3s agents as a distributed file system. You need to set a zone affinity for each agent and disable the affinity flag on longhorn. Each replica will be spread across the zones evenly so that a replica can't be on the same physical server. The storage space of all the agents should not exceed more then 50% of all available space totaled on all PROXMOX servers combined, this ensures that if a proxmox node needs to be shutdown, all the agents can be migrated to other proxmox machines for a short period and still function without running out of storage space.  
**NOTE:** The `./deploy.sh` script will set the zone affinty to the `cluster.yaml` `proxmox: zone` name and append the last proxmox ip segment to the name. This can also be used later to determine which nodes need to be migrated back to which server once you bring it back up.

1. Set the helm charts and install longhorn with zone affinity off.
```shell
helm repo add longhorn https://charts.longhorn.io
helm repo update
helm install longhorn longhorn/longhorn --namespace longhorn-system --create-namespace --version 1.5.1 --set defaultSettings.zoneAntiAffinity=false
```
2. Deploy longhorn dashboard.
**NOTE:** Edit the ingress.yaml first.
```shell
kubectl apply -f longhorn
```
3. You can test longhorn by deploying a test PVC to make sure it allocates it (note this can take several minutes until longhorn is fully init'ed).
```shell
kubectl apply -f longhorn/test
```
You can visit `longhorn.gladeos.dev` and wait for the PVC to be created, it should eventually show 1 volume attached. You can remove the test with
```shell
kubectl delete -f longhorn/test
```
## 11. Loki 
This is a centeralized log system that will capture all standard output logs from pods and allow them to be queryable.

1. Install the helm charts
```shell
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update
```

## Finalize <a id="finalize"></a>
You should spend locking down the security, for example
- disable password ssh access to proxmox
- check pod security settings
- double check the forward-auth middleware is set for all internal tools
- check that the cronjob for ip-whitelist succeeded by this point
- make sure you enabled cert-manager replica to 3 in `cert-manager/values.yaml` and then ran `helm upgrade cert-manager jetstack/cert-manager -n cert-manager -f cert-manager/values.yaml`
- make sure traefik is set to 3 replicas if you reduced it to debug `helm upgrade traefik traefik/traefik -n traefik -f traefik/values.yaml`