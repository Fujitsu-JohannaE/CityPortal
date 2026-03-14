# CityPortal — Azure Infrastructure

## Prerequisites

1. [Terraform CLI](https://developer.hashicorp.com/terraform/install) (≥ 1.5)
2. [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) — logged in (`az login`)

## Quick start

```bash
cd infra

# 1. Create your variables file from the example
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars — fill in subscription_id and sql_admin_password

# 2. Initialize Terraform (downloads Azure provider)
terraform init

# 3. Preview what will be created
terraform plan

# 4. Deploy
terraform apply

# 5. Deploy the .NET app
az webapp deploy \
  --resource-group $(terraform output -raw resource_group_name) \
  --name $(terraform output -raw app_service_name) \
  --src-path ../publish.zip

# 6. Open in browser
echo $(terraform output -raw app_service_url)/vantaa/forms
```

## What gets created

| Resource | Name pattern | Purpose |
|----------|-------------|---------|
| Resource Group | `rg-cityportal-dev` | Container for all resources |
| SQL Server | `sql-cityportal-dev` | Azure SQL logical server |
| SQL Database | `sqldb-cityportal` | Single DB (Basic/5 DTU) — all tenants share it |
| Storage Account | `stcityportalfilesdev` | Shared blob storage for attachments |
| Storage Container | `form-attachments` | Private container, path-isolated per tenant |
| Defender for Storage | — | Malware scanning on upload |
| App Service Plan | `plan-cityportal-dev` | Linux B1 plan |
| App Service | `app-cityportal-dev` | .NET 8 web app with Managed Identity |
| RBAC | Blob Data Contributor + Reader | App → Storage auth |

## Estimated cost (dev)

~€15–20/month (B1 App Service + Basic SQL + Standard LRS Storage)

## Tear down

```bash
terraform destroy
```
