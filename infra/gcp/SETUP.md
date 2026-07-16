# GCP Cloud Run — One-time Setup Guide

Follow these steps once per new GCP project. After this, every deploy is automatic via GitHub Actions.

## Prerequisites

- [gcloud CLI](https://cloud.google.com/sdk/docs/install) installed
- A GCP project with billing enabled
- Terraform installed: `brew tap hashicorp/tap && brew install hashicorp/tap/terraform`

---

## Phase 1 — Log in

```bash
gcloud auth login
gcloud auth application-default login
gcloud config set project YOUR_PROJECT_ID
```

Terraform creates everything from here — service account, IAM roles, Workload Identity Federation, Artifact Registry, and Cloud Run.

---

## Phase 2 — Terraform

**Create `infra/gcp/terraform.tfvars`** (gitignored — never commit it):
```bash
cp infra/gcp/terraform.tfvars.example infra/gcp/terraform.tfvars
```

Fill in your values. Get your project ID with:
```bash
gcloud config get project
```

> **Mollie OAuth credentials (feature 014a — invoice payments):** before filling in
> `mollie_client_id`/`mollie_client_secret`, register an OAuth app at
> [my.mollie.com](https://my.mollie.com) → **Developers → OAuth clients** (or **Apps** if
> you're a registered Mollie Connect partner). Set `mollie_redirect_uri` to your director-web
> domain's `/settings/payment-connection-callback` path and register that exact same URL as
> the app's allowed redirect URL in Mollie's dashboard — the OAuth flow fails if they don't
> match. Use Mollie's test-mode credentials until you're ready to accept real payments.

**Run Terraform:**
```bash
cd infra/gcp
terraform init
terraform plan
terraform apply
```

> **First apply note:** The `image` variable defaults to a public Google placeholder — leave it as-is. GitHub Actions will push the real image and update Cloud Run on its first run.

Copy the outputs — you need them for GitHub secrets:
```
service_url                = "https://childcare-api-xxxx-ew.a.run.app"
artifact_registry_repo     = "europe-west1-docker.pkg.dev/YOUR_PROJECT/childcare"
workload_identity_provider = "projects/.../providers/github-actions"
service_account            = "github-actions@YOUR_PROJECT.iam.gserviceaccount.com"
```

---

## Phase 3 — GitHub secrets

Go to: **GitHub repo → Settings → Secrets and variables → Actions → New repository secret**

| Secret name | Value |
|---|---|
| `GCP_PROJECT_ID` | Your GCP project ID |
| `GCP_REGION` | Your region (e.g. `europe-west1`) |
| `GCP_WORKLOAD_IDENTITY_PROVIDER` | `workload_identity_provider` from Terraform output |
| `GCP_SERVICE_ACCOUNT` | `service_account` from Terraform output |
| `DB_CONNECTION_STRING` | Your Supabase connection string |
| `JWT_SECRET` | Your JWT secret |
| `GOOGLE_ANDROID_CLIENT_ID` | Google OAuth Android client ID |
| `GOOGLE_IOS_CLIENT_ID` | Google OAuth iOS client ID |
| `GOOGLE_WEB_CLIENT_ID` | Google OAuth Web client ID |
| `APPLE_BUNDLE_ID` | Your iOS bundle identifier |
| `MOLLIE_CLIENT_ID` | Mollie OAuth client ID (see the Mollie note above) |
| `MOLLIE_CLIENT_SECRET` | Mollie OAuth client secret |
| `MOLLIE_REDIRECT_URI` | Same value as `mollie_redirect_uri` in `terraform.tfvars` |
| `APP_API_BASE_URL` | Same value as `app_api_base_url` in `terraform.tfvars` (`service_url` from Terraform output) |

---

## Phase 4 — First deploy

Push any change to a file inside `backend/` on the `master` branch. The `deploy-gcp.yml` workflow will trigger automatically and your API will be live at the `service_url` from the Terraform output.

---

## Subsequent deploys

No action needed — every push to `master` that touches `backend/**` triggers an automatic deploy. Terraform is only needed again if the infrastructure changes.

---

## Note — Terraform state

By default Terraform stores state in a local `terraform.tfstate` file. This is fine for a solo developer. If you ever add a collaborator or run `terraform apply` from CI, configure a remote backend first (e.g. a GCS bucket) so state is shared and locked:

```hcl
# infra/gcp/main.tf — add at the top, inside terraform {}
terraform {
  backend "gcs" {
    bucket = "YOUR_BUCKET_NAME"
    prefix = "childcare/gcp"
  }
}
```

Then run `terraform init -migrate-state` to move the existing local state into the bucket.
