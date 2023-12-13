The idea is to disable the default ingress + service and create our own.
1. IngressRoute will have middleware for github oauth
2. Service will be shortened and used internally on the cluster


```shell
helm repo add sonatype https://sonatype.github.io/helm3-charts/
helm repo update
helm install nexus sonatype/nexus-repository-manager --namespace nexus -f values.yaml --create-namespace
```

```shell
helm upgrade nexus sonatype/nexus-repository-manager --values=values.yaml
```