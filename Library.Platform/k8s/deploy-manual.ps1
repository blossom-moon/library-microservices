<#
 Лаб. 10, Частина 2–3: ручне розгортання gateway і catalog-api (разом з інфраструктурою, потрібною Каталогу).
 Виконувати з теки Library.Platform. Перед цим: aspirate destroy (імена Deployment збігаються з тими, що створює aspirate).
#>
$ErrorActionPreference = 'Stop'

# 1. Образи (локально зібрані образи Docker Desktop доступні кластеру без реєстру → imagePullPolicy: IfNotPresent)
docker build -t catalog-api:v1 -f Services/Catalog/Library.Catalog.Api/Dockerfile .
docker build -t gateway:v1 -f Platform.ApiGateway/Dockerfile .

# 2. Secrets (значення — зі змінних оточення, у репозиторій не потрапляють)
./k8s/create-secrets.ps1

# 3. Інфраструктура, ConfigMap, Deployment + Service
kubectl apply -f k8s/infra/
kubectl apply -f k8s/gateway-configmap.yaml -f k8s/catalog-configmap.yaml
kubectl apply -f k8s/catalog-api-deployment.yaml -f k8s/catalog-api-service.yaml
kubectl apply -f k8s/gateway-deployment.yaml -f k8s/gateway-service.yaml

kubectl get pods,services
Write-Host "`nДалі: kubectl port-forward service/gateway 8080:80   та   ./scripts/make-jwt.ps1"
