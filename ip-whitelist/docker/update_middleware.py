from kubernetes import client, config
import yaml
import socket
import logging

logging.basicConfig(level=logging.INFO)

def domain_to_ip_with_cidr(domain, cidr_suffix='/32'):
    try:
        ip_address = socket.gethostbyname(domain)
        logging.info(f"Resovled '{domain}' to '{ip_address}{cidr_suffix}'")
        return f"{ip_address}{cidr_suffix}"
    except socket.gaierror:
        logging.error(f"Failed to resolve {domain}")
        return None

# Initialize the Kubernetes client
config.load_incluster_config()

v1 = client.CoreV1Api()
api_instance = client.CustomObjectsApi()

# Name of the ConfigMap
configmap_name = 'ip-whitelist'
namespace = 'default'

# Fetch the ConfigMap
configmap = v1.read_namespaced_config_map(name=configmap_name, namespace=namespace)
domain_list = yaml.safe_load(configmap.data.get('domains', '[]'))  # 'domains' contains the YAML array of domains
ip_list_from_config = yaml.safe_load(configmap.data.get('ips', '[]'))  # 'ips' contains the YAML array of IPs

# Convert domains to IPs
ip_list = []
for domain in domain_list:
    ip_cidr = domain_to_ip_with_cidr(domain)
    if ip_cidr:
        ip_list.append(ip_cidr)

# Add IPs from ConfigMap
for ip_address in ip_list_from_config:
    logging.info(f"Appending '{ip_address}'")

ip_list.extend(ip_list_from_config)

if not ip_list:
    logging.error("No IPs found. Exiting.")
    sys.exit(1)

# Middleware details
group = 'traefik.containo.us'
version = 'v1alpha1'
plural = 'middlewares'
middleware_name = 'ip-whitelist'

# Fetch existing Middleware
existing_middleware = api_instance.get_namespaced_custom_object(
    group, version, namespace, plural, middleware_name
)
resource_version = existing_middleware['metadata']['resourceVersion']

# Update Middleware
body = {
    'apiVersion': 'traefik.containo.us/v1alpha1',
    'kind': 'Middleware',
    'metadata': {
        'name': middleware_name,
        'resourceVersion': resource_version  # Include the resourceVersion
    },
    'spec': {
        'ipWhiteList': {
            'sourceRange': ip_list
        }
    }
}

api_instance.replace_namespaced_custom_object(group, version, namespace, plural, middleware_name, body)
