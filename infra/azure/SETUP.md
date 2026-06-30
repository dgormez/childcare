# Azure Container Apps — One-time Setup Guide

Follow these steps once per new Azure subscription. After this, every deploy is automatic via GitHub Actions.

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- An Azure subscription with billing enabled
- Terraform installed: `brew tap hashicorp/tap && brew install hashicorp/tap/terraform`

---

## Phase 1 — Log in

```bash
az login
az provider register --namespace Microsoft.App --wait
```

The second command registers the Container Apps service on your subscription — it's a one-time step per subscription and takes about 30 seconds. Terraform creates everything else.

---

## Phase 2 — Terraform

Terraform creates: Container Registry, Container Apps environment, Container App, a User Assigned Managed Identity for GitHub Actions, its role assignments (Contributor + AcrPush), and the federated identity credential — no Azure portal needed.

**Create `infra/azure/terraform.tfvars`** (gitignored — never commit it):
```bash
cp infra/azure/terraform.tfvars.example infra/azure/terraform.tfvars
```

Fill in your values. Get your subscription ID with:
```bash
az account show --query id -o tsv
```

**Run Terraform:**
```bash
cd infra/azure
terraform init
terraform plan
terraform apply
```

Copy the outputs — you need them for GitHub secrets:
```
app_url          = "https://childcare-api-xxxx.northeurope.azurecontainerapps.io"
acr_login_server = "childcareregistry.azurecr.io"
azure_client_id  = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
azure_tenant_id  = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
```

---

## Phase 3 — GitHub secrets

Go to: **GitHub repo → Settings → Secrets and variables → Actions → New repository secret**

| Secret name | Value |
|---|---|
| `AZURE_CLIENT_ID` | `azure_client_id` from Terraform output |
| `AZURE_TENANT_ID` | `azure_tenant_id` from Terraform output |
| `AZURE_SUBSCRIPTION_ID` | Your Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | `childcare-rg` |
| `AZURE_CONTAINER_APP_NAME` | `childcare-api` |
| `ACR_LOGIN_SERVER` | `acr_login_server` from Terraform output |
| `DB_CONNECTION_STRING` | Your Supabase connection string |
| `JWT_SECRET` | Your JWT secret |
| `GOOGLE_ANDROID_CLIENT_ID` | Google OAuth Android client ID |
| `GOOGLE_IOS_CLIENT_ID` | Google OAuth iOS client ID |
| `GOOGLE_WEB_CLIENT_ID` | Google OAuth Web client ID |
| `APPLE_BUNDLE_ID` | Your iOS bundle identifier |

---

## Phase 4 — First deploy

Push any change to a file inside `backend/` on the `master` branch. The `deploy-azure.yml` workflow will trigger automatically and your API will be live at the `app_url` from the Terraform output.

---

## Subsequent deploys

No action needed — every push to `master` that touches `backend/**` triggers an automatic deploy. Terraform is only needed again if the infrastructure changes.

---

## Note — Terraform state

By default Terraform stores state in a local `terraform.tfstate` file. This is fine for a solo developer. If you ever add a collaborator or run `terraform apply` from CI, configure a remote backend first (e.g. an Azure Blob Storage container) so state is shared and locked:

```hcl
# infra/azure/main.tf — add at the top, inside terraform {}
terraform {
  backend "azurerm" {
    resource_group_name  = "YOUR_RESOURCE_GROUP"
    storage_account_name = "YOUR_STORAGE_ACCOUNT"
    container_name       = "tfstate"
    key                  = "childcare/azure.tfstate"
  }
}
```

Then run `terraform init -migrate-state` to move the existing local state into the storage container.
