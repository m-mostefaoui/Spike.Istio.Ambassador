# Kubernetes + Istio + Ambassador (Edge Proxy and Service Mesh)

## Google Cloud Platform Setup

Docker for Windows or Minikube do not implement load balancer required by the
Istio, so we will need to use a real Kubernetes cluster in Google Cloud Platform.

### Kubernetes Cluster

Start Google Cloud Shell console.

Create cluster and get credentials

```
gcloud container clusters create pvr-spike-istio-ambassador \
 --cluster-version=1.9.7 \
 --zone europe-west4-b \
 --num-nodes 3
 --machine-type=n1-standard-2
gcloud container clusters get-credentials pvr-spike-istio-ambassador \
 --zone=europe-west4-b \
 --project=parcel-vision
```

Set permissions required to setup Istio

```
kubectl create clusterrolebinding cluster-admin-binding --clusterrole=cluster-admin --user=$(gcloud config get-value core/account)
```

## Istio

Download Istio
```
wget https://github.com/istio/istio/releases/download/0.8.0/istio-0.8.0-linux.tar.gz
tar xvfz istio-0.8.0-linux.tar.gz
```

Install using Helm
```
kubectl create ns istio-system
helm install install/kubernetes/helm/istio --name istio --namespace istio-system --set tracing.enabled=true
```
Check the progress of the deployment (every service should have an `AVAILABLE` count of `1`):
```
kubectl get deployments -n istio-system
NAME                       DESIRED   CURRENT   UP-TO-DATE   AVAILABLE   AGE
grafana                    1         1         1            1           6d
istio-citadel              1         1         1            1           6d
istio-egressgateway        1         1         1            1           6d
istio-galley               1         1         1            1           6d
istio-ingressgateway       1         1         1            1           6d
istio-pilot                1         1         1            1           6d
istio-policy               1         1         1            1           6d
istio-sidecar-injector     1         1         1            1           6d
istio-statsd-prom-bridge   1         1         1            1           6d
istio-telemetry            1         1         1            1           6d
istio-tracing              1         1         1            1           6d
prometheus                 1         1         1            1           6d
servicegraph               1         1         1            1           6d                                                               
```
## Ambassador

### Install Ambassador

Start Google Cloud Shell, and follow installation steps from <https://www.getambassador.io/user-guide/getting-started>,
section "Deploying Ambassador to Kubernetes".

To run the command from your local console (see <https://cloud.google.com/sdk/gcloud/reference/container/clusters/get-credentials>):
```
gcloud container clusters get-credentials pvr-spike-ambassador --zone europe-west2-a --project parcel-vision
```

Note: In the link above there is a section describing how to check if Kubernetes has RBAC enabled, and running different versions of install script based on the result. The non-RBAC Ambassador script may fail even if the RBAC is not enabled. In this case, run the RBAC variant:

We need to deploy the Ambassador ambassador-admin service to our cluster:
```
kubectl apply -f https://getambassador.io/yaml/ambassador/ambassador-rbac.yaml
```

If you're using Google Kubernetes Engine with RBAC, you'll need to grant permissions to the account that will be setting up Ambassador. To do this, get your official GKE username, and then grant cluster-admin role privileges to that username:


```
kubectl create clusterrolebinding my-cluster-admin-binding --clusterrole=cluster-admin --user=$(gcloud info --format="value(config.account)")
```

NOTE: It's possible if you run this command from your local console command, it will not work.
Use the Google Cloud Shell.

## Authentication for Docker Containers Registry

```
gcloud auth configure-docker
```

## Spike Components
Ambassador and Istio are deployed together on KubernetesIn this configuration, incoming traffic from outside the cluster is first routed through Ambassador, which then routes the traffic to Istio-powered services. Ambassador handles authentication, edge routing, TLS termination, and other traditional edge functions.
### Test simple route

Test if the Ambassador is working. Create a configuration that will
redirect request to an external service (`httpbin.org`):

`httpbin.yaml`:

```yaml
---
apiVersion: v1
kind: Service
metadata:
  name: httpbin
  annotations:
    getambassador.io/config: |
      ---
      apiVersion: ambassador/v0
      kind:  Mapping
      name:  httpbin_mapping
      prefix: /httpbin/
      service: httpbin.org:80
      host_rewrite: httpbin.org
spec:
  ports:
  - name: httpbin
    port: 80
```
Deploy the configuration:

```
kubectl apply -f httpbin.yaml
```

Find out the external IP of the Ambassador service:

```
kubectl get svc -o wide ambassador
```

Example output:

```
NAME         TYPE           CLUSTER-IP      EXTERNAL-IP     PORT(S)        AGE       SELECTOR
ambassador   LoadBalancer   10.11.248.237   35.204.54.120   80:30893/TCP   2d        service=ambassador
```

In this case, the IP is `35.204.54.120`.

Test the route by sending a `GET` request to `http://35.204.54.120/httpbin/ip` (using curl or Postman).
The response should have status `200 OK` and body similar to this:

```
{
    "origin": "167.98.11.74"
}
```
### Load Balancing Capabilities

We need to deploy an ambassador service that acts as a point of ingress into the cluster via the LoadBalancer type. Create the following YAMl and put it in a file `ambassador-http.yaml`.  
Note that we have added `getambassador.io/config` annotation on the service, and use the `Mapping` contained in it to configure the route.   
In this case, the mapping creates a route that will route traffic from `/pvrapi/` to `pvrapi:8010`
```yaml
---
apiVersion: v1
kind: Service
metadata:
  labels:
    service: ambassador
  name: ambassador
  annotations:
    getambassador.io/config: |
      ---
      apiVersion: ambassador/v0
      kind:  Mapping
      name:  pvrapi_mapping
      prefix: /pvrapi/
      service: pvrapi:8010
spec:
  type: LoadBalancer
  ports:
  - name: ambassador
    port: 80
    targetPort: 80
  selector:
    service: ambassador
```

Then, apply it to the Kubernetes with kubectl:

```
kubectl apply -f ambassador-http.yaml
```
Create the following YAML and put it in a file `pvr-application.yaml`
```yaml
---
# api service

apiVersion: v1
kind: Service
metadata:
  name: pvrapi
  labels:
    app: pvrapi
spec:
  type: ClusterIP
  selector:
    app: pvrapi
  ports:
  - protocol: TCP
    port: 8010
    name: http

---
apiVersion: v1
kind: ReplicationController
metadata:
  name: pvrapi
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: pvrapi
    spec:
      containers:
      - name: pvr-pvrapi
        image: eu.gcr.io/parcel-vision/spike-istio-ambassador-api:1.0
        ports:
        - containerPort: 8010

---
# Application service

apiVersion: v1
kind: Service
metadata:
  name: pvr-app
  labels:
    app: pvr-app
spec:
  ports:
  - port: 8020
    name: http
  selector:
    app: pvr-app
---
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: app-v1
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: pvr-app
        version: v1
    spec:
      containers:
      - name: pvr-app
        image: eu.gcr.io/parcel-vision/spike-istio-ambassador-application-v1:1.0
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8020
---
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: app-v2
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: pvr-app
        version: v2
    spec:
      containers:
      - name: pvr-app
        image: eu.gcr.io/parcel-vision/spike-istio-ambassador-application-v2:1.0
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8020
```
To test things out, we'll need the external IP for Ambassador:
```
kubectl get svc -o wide ambassador
NAME         TYPE           CLUSTER-IP      EXTERNAL-IP      PORT(S)        AGE       SELECTOR
ambassador   LoadBalancer   10.51.250.209   35.195.116.189   80:30164/TCP   1d        service=ambassador
```
You should now be able to use curl
```
curl 35.36.37.38/pvrapi/api/values/
```

### Canary Capabilities

Ambassador supports fine-grained canary releases. Here's an example.
```yaml
---
apiVersion: v1
kind: Service
metadata:
  labels:
    service: ambassador
  name: ambassador
  annotations:
    getambassador.io/config: |
      ---
      apiVersion: ambassador/v0
      kind:  Mapping
      name:  apiv1_mapping
      prefix: /pvrapi/
      service: apiv1:8010
      ---
      apiVersion: ambassador/v0
      kind:  Mapping
      name:  apiv2_mapping
      prefix: /pvrapi/
      weight: 50
      service: apiv2:8010
spec:
  type: LoadBalancer
  ports:
  - name: ambassador
    port: 80
    targetPort: 80
  selector:
    service: ambassador

```

Then, apply it to the Kubernetes with kubectl:

```
kubectl apply -f ambassador-http.yaml
```

In this case, the `apiv2_mapping` will receive 50% of the requests for `/pvrapi/`, and Ambassador will assign the remaining 50% to the `qotm_mapping`

### TLS Termination
To enable TLS termination for Ambassador you'll need a few things:

1. You'll need a TLS certificate.  
2. For any production use, you'll need a DNS record that matches your TLS certificate's `Common Name`.  
3. You'll need to store the certificate in a Kubernetes secret.  
4. You may need to configure other Ambassador TLS options using the tls module.

(<https://www.getambassador.io/user-guide/tls-termination>)

Create the following YAML and put it in a file `ambassador-https.yaml`

```yaml
---
apiVersion: v1
kind: Service
metadata:
  creationTimestamp: null
  labels:
    service: ambassador
  name: ambassador
  annotations:
    getambassador.io/config: |
      ---
      apiVersion: ambassador/v0
      kind:  Mapping
      name:  pvrapi_mapping
      prefix: /pvrapi/
      service: apiv1:8010
      ---
      apiVersion: ambassador/v0
      kind: Module
      name: tls
      config:
        server:
          enabled: True
          redirect_cleartext_from: 80
spec:
  type: LoadBalancer
  ports:
  - name: ambassador-http
    port: 80
    targetPort: 80
  - name: ambassador-https
    port: 443
    targetPort: 443
  selector:
    service: ambassador


```

Then, apply it to the Kubernetes with kubectl:

```
kubectl apply -f ambassador-https.yaml
```

Create a Kubernetes `secret` named `ambassador-certs`:

```
kubectl create secret tls ambassador-certs --cert=$FULLCHAIN_PATH --key=$PRIVKEY_PATH
```

```
kubectl get secrets | grep ambassador-certs
ambassador-certs                  kubernetes.io/tls                     2         7d
```

Verify that the TLS Termination is enabled, execute the following command:
```
kubectl get svc -o wide ambassador
NAME         TYPE           CLUSTER-IP      EXTERNAL-IP      PORT(S)        AGE       SELECTOR
ambassador   LoadBalancer   10.51.250.209   35.195.116.189   80:30164/TCP   2d        service=ambassador
```

Open `Postman`.
Send a `GET` request to https://35.195.116.189/pvrapi/api/values/ and verify yuou have a succesfull response.

### Ambassador Diagnostics
If you want to look at the Ambassador Diagnostic UI then you can use port-forwarding.  
First you will need to find the name of an ambassador pod:
```
kubectl get pods
```
```
kubectl get pods
NAME                                   READY     STATUS    RESTARTS   AGE
ambassador-78965fbd79-vxhvj            2/2       Running   0          7d
apiv1-nt57f                            1/1       Running   0          7d
apiv2-s8qmr                            1/1       Running   0          7d
appv1-jqqql                            1/1       Running   0          7d
appv2-g9p7b                            1/1       Running   0          7d
prometheus-operator-8697c7fff9-w4qkq   1/1       Running   0          8d
prometheus-prometheus-0                2/2       Running   0          8d
```

Here we’ll pick `ambassador-78965fbd79-vxhvj`. You can now port-forward from your local network adapter to inside the cluster and expose the Ambassador Diagnostic UI that is running on port `8877`.

```
kubectl port-forward ambassador-78965fbd79-vxhvj 8877:8877
```
You can now visit http://localhost:8877/ambassador/v0/diag in your browser and have a look around!


### Tracing without Istio installed (Grafana / Prometheus)
To be able to use `Prometheus` & `Grafana` with only `Ambassador` installed you need to create the following YAML files and apply them.  

 `ambassador-rbac.yaml`
 ```yaml
---
apiVersion: v1
kind: Service
metadata:
  labels:
    service: ambassador-admin
  name: ambassador-admin
spec:
  type: NodePort
  ports:
  - name: ambassador-admin
    port: 8877
    targetPort: 8877
  selector:
    service: ambassador
---
apiVersion: rbac.authorization.k8s.io/v1beta1
kind: ClusterRole
metadata:
  name: ambassador
rules:
- apiGroups: [""]
  resources:
  - services
  verbs: ["get", "list", "watch"]
- apiGroups: [""]
  resources:
  - configmaps
  verbs: ["create", "update", "patch", "get", "list", "watch"]
- apiGroups: [""]
  resources:
  - secrets
  verbs: ["get", "list", "watch"]
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: ambassador
---
apiVersion: rbac.authorization.k8s.io/v1beta1
kind: ClusterRoleBinding
metadata:
  name: ambassador
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: ambassador
subjects:
- kind: ServiceAccount
  name: ambassador
  namespace: default
---
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: ambassador
spec:
  replicas: 1
  template:
    metadata:
      labels:
        service: ambassador
    spec:
      serviceAccountName: ambassador
      containers:
      - name: ambassador
        image: datawire/ambassador:0.21.0
        imagePullPolicy: Always
        resources:
          limits:
            cpu: 1
            memory: 400Mi
          requests:
            cpu: 200m
            memory: 100Mi
        env:
        - name: AMBASSADOR_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
        livenessProbe:
          httpGet:
            path: /ambassador/v0/check_alive
            port: 8877
          initialDelaySeconds: 3
          periodSeconds: 3
        readinessProbe:
          httpGet:
            path: /ambassador/v0/check_ready
            port: 8877
          initialDelaySeconds: 3
          periodSeconds: 3
      - name: statsd-sink
        image: datawire/prom-statsd-exporter:0.6.0
      restartPolicy: Always
```
```
kubectl apply -f ambassador-rbac.yaml
```

`prom-operator.yaml`
 ```yaml
apiVersion: rbac.authorization.k8s.io/v1beta1
kind: ClusterRoleBinding
metadata:
  name: prometheus-operator
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: prometheus-operator
subjects:
- kind: ServiceAccount
  name: prometheus-operator
  namespace: default
---
apiVersion: rbac.authorization.k8s.io/v1beta1
kind: ClusterRole
metadata:
  name: prometheus-operator
rules:
- apiGroups:
  - extensions
  resources:
  - thirdpartyresources
  verbs:
  - "*"
- apiGroups:
  - apiextensions.k8s.io
  resources:
  - customresourcedefinitions
  verbs:
  - "*"
- apiGroups:
  - monitoring.coreos.com
  resources:
  - alertmanagers
  - prometheuses
  - servicemonitors
  verbs:
  - "*"
- apiGroups:
  - apps
  resources:
  - statefulsets
  verbs: ["*"]
- apiGroups: [""]
  resources:
  - configmaps
  - secrets
  verbs: ["*"]
- apiGroups: [""]
  resources:
  - pods
  verbs: ["list", "delete"]
- apiGroups: [""]
  resources:
  - services
  - endpoints
  verbs: ["get", "create", "update"]
- apiGroups: [""]
  resources:
  - nodes
  verbs: ["list", "watch"]
- apiGroups: [""]
  resources:
  - namespaces
  verbs: ["list"]
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: prometheus-operator
---
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  labels:
    k8s-app: prometheus-operator
  name: prometheus-operator
spec:
  replicas: 1
  template:
    metadata:
      labels:
        k8s-app: prometheus-operator
    spec:
      containers:
      - args:
        - --kubelet-service=kube-system/kubelet
        - --config-reloader-image=quay.io/coreos/configmap-reload:v0.0.1
        image: quay.io/coreos/prometheus-operator:v0.15.0
        name: prometheus-operator
        ports:
        - containerPort: 8080
          name: http
        resources:
          limits:
            cpu: 200m
            memory: 100Mi
          requests:
            cpu: 100m
            memory: 50Mi
      serviceAccountName: prometheus-operator
```
```
kubectl apply -f prom-operator.yaml
```

`prom-rbac.yaml`
 ```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: prometheus
---
apiVersion: rbac.authorization.k8s.io/v1beta1
kind: ClusterRole
metadata:
  name: prometheus
rules:
- apiGroups: [""]
  resources:
  - nodes
  - services
  - endpoints
  - pods
  verbs: ["get", "list", "watch"]
- apiGroups: [""]
  resources:
  - configmaps
  verbs: ["get"]
- nonResourceURLs: ["/metrics"]
  verbs: ["get"]
---
apiVersion: rbac.authorization.k8s.io/v1beta1
kind: ClusterRoleBinding
metadata:
  name: prometheus
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: prometheus
subjects:
- kind: ServiceAccount
  name: prometheus
  namespace: default
```

```
kubectl apply -f prom-rbac.yaml
```

`prom-svc.yaml`
 ```yaml
apiVersion: v1
kind: Service
metadata:
  name: prometheus
spec:
  type: NodePort
  ports:
  - name: web
    port: 9090
    protocol: TCP
    targetPort: web
  selector:
    prometheus: prometheus
```
```
kubectl apply -f prom-svc.yaml
```

`prometheus.yaml`
 ```yaml
apiVersion: monitoring.coreos.com/v1
kind: Prometheus
metadata:
  name: prometheus
spec:
  serviceAccountName: prometheus
  serviceMonitorSelector:
    matchLabels:
      ambassador: monitoring
  resources:
    requests:
      memory: 400Mi
```
```
kubectl apply -f prometheus.yaml
```

`statsd-sink-svc.yaml`
 ```yaml
---
apiVersion: v1
kind: Service
metadata:
  name: ambassador-monitor
  labels:
    service: ambassador-monitor
spec:
  selector:
    service: ambassador
  type: ClusterIP
  clusterIP: None
  ports:
  - name: prometheus-metrics
    port: 9102
    targetPort: 9102
    protocol: TCP
---
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: ambassador-monitor
  labels:
    ambassador: monitoring
spec:
  selector:
    matchLabels:
      service: ambassador-monitor
  endpoints:
  - port: prometheus-metrics
```
```
kubectl apply -f statsd-sink-svc.yaml
```


### Tracing with Istio installed
#### Prometheus
```
kubectl -n istio-system get pod -l app=prometheus
NAME                          READY     STATUS    RESTARTS   AGE
prometheus-7b6f8b9996-mwbmj   1/1       Running   0          7d
```
```
kubectl -n istio-system port-forward prometheus-7b6f8b9996-mwbmj  9090:9090
```

Visit http://localhost:9090/graph in your web browser.

#### Jaeger
```
kubectl -n istio-system get pod -l app=jaeger
NAME                           READY     STATUS    RESTARTS   AGE
istio-tracing-5fbd79cc-5wswz   1/1       Running   0          7d
```
```
kubectl -n istio-system port-forward istio-tracing-5fbd79cc-5wswz 16686:16686
```
Visit http://localhost:16686 in your web browser.


## Ambassador + Istio
### Canary Deployments using Istio
We deploy an ambassador service that acts as a point of ingress into the cluster via the LoadBalancer type.  
 Create the following YAML and put it in a file called `ambassador-http.yaml`.

```yaml
---
apiVersion: v1
kind: Service
metadata:
  labels:
    service: ambassador
  name: ambassador
  annotations:
    getambassador.io/config: |
      ---
      apiVersion: ambassador/v0
      kind:  Mapping
      name:  pvrapi_mapping
      prefix: /pvrapi/
      service: pvrapi:8010
spec:
  type: LoadBalancer
  ports:
  - name: ambassador
    port: 80
    targetPort: 80
  selector:
    service: ambassador
```
Then, apply it to the Kubernetes 
```
kubectl apply -f ambassador-http.yaml
```
Create the following YAML and put in file called `pvr-application.yaml`.

```yaml
---
# api service

apiVersion: v1
kind: Service
metadata:
  name: pvrapi
  labels:
    app: pvrapi
spec:
  type: ClusterIP
  selector:
    app: pvrapi
  ports:
  - protocol: TCP
    port: 8010
    name: http

---
apiVersion: v1
kind: ReplicationController
metadata:
  name: pvrapi
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: pvrapi
    spec:
      containers:
      - name: pvr-pvrapi
        image: eu.gcr.io/parcel-vision/spike-istio-ambassador-api:1.0
        ports:
        - containerPort: 8010

---
# Application service

apiVersion: v1
kind: Service
metadata:
  name: pvr-app
  labels:
    app: pvr-app
spec:
  ports:
  - port: 8020
    name: http
  selector:
    app: pvr-app
---
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: app-v1
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: pvr-app
        version: v1
    spec:
      containers:
      - name: pvr-app
        image: eu.gcr.io/parcel-vision/spike-istio-ambassador-application-v1:1.0
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8020
---
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: app-v2
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: pvr-app
        version: v2
    spec:
      containers:
      - name: pvr-app
        image: eu.gcr.io/parcel-vision/spike-istio-ambassador-application-v2:1.0
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8020
```
Then, apply it to the Kubernetes 
```
kubectl apply -f pvr-application.yaml
```
For example if we want to send 10% of the traffic to the canary,
Create the following YAML and put in file called `pvr-application-canary.yaml`.
```yaml
---
apiVersion: networking.istio.io/v1alpha3
kind: VirtualService
metadata:
  name: pvr-app
  namespace: default
spec:
  hosts:
    - pvr-app
  http:
  - route:
    - destination:
        host: pvr-app
        subset: app-v1
      weight: 90
    - destination:
        host: pvr-app
        subset: app-v2
      weight: 10

---
apiVersion: networking.istio.io/v1alpha3
kind: DestinationRule
metadata:
  name: pvr-app
  namespace: default
spec:
  host: pvr-app 
  subsets:
  - name: app-v1
    labels:
      version: v1
  - name: app-v2
    labels:
      version: v2
```
Then, apply it to the Kubernetes 
```
kubectl apply -f pvr-application-canary.yaml
```

After setting this rule, Istio will ensure that only one tenth of the requests will be sent to the canary version, regardless of how many replicas of each version are running.


### More sophisticated canary deployment scenarios using Istio

Assume that we have `application v1` that called `config-service v1` and  `application v2` that called `config-service v2`.   
We want to control the trafic to `config-service`. We can do that by sending in request `headers`  which `config-service` version needs to be used with every `application`.   
For the purpose of our demo, the `C#` code for the `api` and `application` has been modified to take care about the `config-service` version.

Create the following YAML and put in file called `config-service.yaml`.

```yaml
---
# config service
apiVersion: v1
kind: Service
metadata:
  name: config-service
spec:
  type: ClusterIP
  selector:
    app: config-service
  ports:
  - port: 8030
    protocol: TCP
    name: http
    
---
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: config-service-v1
spec:
  template:
    metadata:
      labels:
        app: config-service
        version: v1
    spec:
      containers:
      - name: config-service
        image: eu.gcr.io/parcel-vision/spike-istio-ambassador-configservice-v1:1.0
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8030

---
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: config-service-v2
spec:
  template:
    metadata:
      labels:
        app: config-service
        version: v2
    spec:
      containers:
      - name: config-service
        image: eu.gcr.io/parcel-vision/spike-istio-ambassador-configservice-v2:1.0
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8030
```

Then, apply it to the Kubernetes 
```
kubectl apply -f config-service.yaml
```
Create the following YAML and put in file called `config-service.yaml`.

```yaml
---
apiVersion: networking.istio.io/v1alpha3
kind: VirtualService
metadata:
  name: config-service
  namespace: default
spec:
  hosts:
  - config-service
  http:
  - match:
    - headers:
        config-version:  
          exact: v2
    route:
    - destination:
        host: config-service
        subset: config-service-v2
  - route:
    - destination:
        host: config-service
        subset: config-service-v1

---
apiVersion: networking.istio.io/v1alpha3
kind: DestinationRule
metadata:
  name: config-service
  namespace: default
spec:
  host: config-service 
  subsets:
  - name: config-service-v1
    labels:
      version: v1
  - name: config-service-v2
    labels:
      version: v2
```
Then, apply it to the Kubernetes 
```
kubectl apply -f config-service.yaml
```

In this case if the request header contains the key-value pair `config-version:v2` it will send the traffic to `config-service v2` otherwise to `config-service v1`.

## Resources

- https://github.com/redhat-developer-demos/istio-tutorial
- https://dzone.com/articles/istio-service-mesh-blog-series-recap
- https://istio.io/docs/tasks/traffic-management/ingress/
- https://istio.io/docs/tasks/telemetry/distributed-tracing/
- https://kubernetes.io/docs/tasks/access-application-cluster/configure-access-multiple-clusters/