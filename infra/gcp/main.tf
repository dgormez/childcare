terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

# APIs

resource "google_project_service" "run" {
  service            = "run.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "artifactregistry" {
  service            = "artifactregistry.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "iam_credentials" {
  service            = "iamcredentials.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "compute" {
  service            = "compute.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "secretmanager" {
  service            = "secretmanager.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "storage" {
  service            = "storage.googleapis.com"
  disable_on_destroy = false
}

# GitHub Actions service account

resource "google_service_account" "github_actions" {
  account_id   = "github-actions"
  display_name = "GitHub Actions"
}

resource "google_project_iam_member" "github_actions_run_admin" {
  project = var.project_id
  role    = "roles/run.admin"
  member  = "serviceAccount:${google_service_account.github_actions.email}"
}

resource "google_project_iam_member" "github_actions_ar_writer" {
  project = var.project_id
  role    = "roles/artifactregistry.writer"
  member  = "serviceAccount:${google_service_account.github_actions.email}"
}

resource "google_project_iam_member" "github_actions_token_creator" {
  project = var.project_id
  role    = "roles/iam.serviceAccountTokenCreator"
  member  = "serviceAccount:${google_service_account.github_actions.email}"
}

data "google_compute_default_service_account" "default" {
  depends_on = [google_project_service.compute]
}

resource "google_service_account_iam_member" "github_actions_act_as" {
  service_account_id = data.google_compute_default_service_account.default.name
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${google_service_account.github_actions.email}"
}

# Workload Identity Federation

resource "google_iam_workload_identity_pool" "github" {
  workload_identity_pool_id = "github-actions-pool"
  display_name              = "GitHub Actions Pool"
}

resource "google_iam_workload_identity_pool_provider" "github_actions" {
  workload_identity_pool_id          = google_iam_workload_identity_pool.github.workload_identity_pool_id
  workload_identity_pool_provider_id = "github-actions"

  oidc {
    issuer_uri = "https://token.actions.githubusercontent.com"
  }

  attribute_mapping = {
    "google.subject"       = "assertion.sub"
    "attribute.repository" = "assertion.repository"
  }

  attribute_condition = "attribute.repository == '${var.github_username}/${var.github_repo}'"
}

resource "google_service_account_iam_member" "github_actions_wif" {
  service_account_id = google_service_account.github_actions.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "principalSet://iam.googleapis.com/${google_iam_workload_identity_pool.github.name}/attribute.repository/${var.github_username}/${var.github_repo}"
}

# Secret Manager
# research.md R11 (feature 001-organisation-onboarding): SUPERADMIN_API_KEY gates the
# temporary Phase 1 invitation-issuance endpoint. Sourced from Secret Manager, not a plain
# Terraform variable interpolated as a literal env value — unlike the existing Jwt/Stripe
# secrets below, which predate this feature and are out of scope to migrate here.

resource "google_secret_manager_secret" "superadmin_api_key" {
  secret_id = "superadmin-api-key"

  replication {
    auto {}
  }

  depends_on = [google_project_service.secretmanager]
}

resource "google_secret_manager_secret_version" "superadmin_api_key" {
  secret      = google_secret_manager_secret.superadmin_api_key.id
  secret_data = var.superadmin_api_key
}

resource "google_secret_manager_secret_iam_member" "superadmin_api_key_accessor" {
  secret_id = google_secret_manager_secret.superadmin_api_key.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${data.google_compute_default_service_account.default.email}"
}

# Cloud Storage
# feature 005-staff, research.md R3: a single bucket for staff profile photos. The API signs
# V4 upload/download URLs directly (no proxying of image bytes), which requires the runtime
# service account (the default compute SA, since Cloud Run doesn't set an explicit one below)
# to be able to sign as itself — roles/iam.serviceAccountTokenCreator granted to its own
# identity — without a downloaded key file (constitution Principle VI: no key file secrets).
# Feature 006-children reuses this same bucket for child photos too (a "children/" path
# prefix distinguishes them from "staff/") — the resource/bucket name keeps its original
# "staff_profile_photos" identifier to avoid a destructive Terraform recreate; only the
# application-facing env var name was generalized (Storage__ProfilePhotosBucketName below).

resource "google_storage_bucket" "staff_profile_photos" {
  name                        = "${var.project_id}-staff-profile-photos"
  location                    = var.region
  uniform_bucket_level_access = true
  force_destroy               = false

  public_access_prevention = "enforced"

  depends_on = [google_project_service.storage]
}

resource "google_storage_bucket_iam_member" "staff_profile_photos_object_admin" {
  bucket = google_storage_bucket.staff_profile_photos.name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${data.google_compute_default_service_account.default.email}"
}

resource "google_service_account_iam_member" "default_sa_token_creator" {
  service_account_id = data.google_compute_default_service_account.default.name
  role               = "roles/iam.serviceAccountTokenCreator"
  member             = "serviceAccount:${data.google_compute_default_service_account.default.email}"
}


# Artifact Registry

resource "google_artifact_registry_repository" "api" {
  location      = var.region
  repository_id = "childcare"
  format        = "DOCKER"
  depends_on    = [google_project_service.artifactregistry]
}

# Cloud Run

resource "google_cloud_run_v2_service" "api" {
  name     = var.service_name
  location = var.region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    containers {
      image = var.image

      ports {
        container_port = 8080
      }

      env {
        name  = "ConnectionStrings__DefaultConnection"
        value = var.db_connection_string
      }
      env {
        name  = "Jwt__Secret"
        value = var.jwt_secret
      }
      env {
        name  = "Jwt__Issuer"
        value = var.jwt_issuer
      }
      env {
        name  = "Jwt__Audience"
        value = var.jwt_audience
      }
      env {
        name  = "Jwt__AccessTokenExpiryMinutes"
        value = "15"
      }
      env {
        name  = "Jwt__RefreshTokenExpiryDays"
        value = "30"
      }
      env {
        name  = "Storage__ProfilePhotosBucketName"
        value = google_storage_bucket.staff_profile_photos.name
      }
      env {
        name  = "Google__AllowedClientIds__0"
        value = var.google_android_client_id
      }
      env {
        name  = "Google__AllowedClientIds__1"
        value = var.google_ios_client_id
      }
      env {
        name  = "Google__AllowedClientIds__2"
        value = var.google_web_client_id
      }
      env {
        name  = "Apple__BundleId"
        value = var.apple_bundle_id
      }
      env {
        name  = "Stripe__SecretKey"
        value = var.stripe_secret_key
      }
      env {
        name  = "Stripe__WebhookSecret"
        value = var.stripe_webhook_secret
      }
      env {
        name  = "Stripe__PriceId"
        value = var.stripe_price_id
      }
      env {
        name = "SuperAdmin__ApiKey"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.superadmin_api_key.secret_id
            version = "latest"
          }
        }
      }
      env {
        name  = "Stripe__SuccessUrl"
        value = "childcare://payment-success"
      }
      env {
        name  = "Stripe__CancelUrl"
        value = "childcare://payment-cancel"
      }
    }
  }

  depends_on = [
    google_artifact_registry_repository.api,
    google_secret_manager_secret_version.superadmin_api_key,
    google_secret_manager_secret_iam_member.superadmin_api_key_accessor,
  ]
}

resource "google_cloud_run_v2_service_iam_member" "public_invoker" {
  project  = google_cloud_run_v2_service.api.project
  location = google_cloud_run_v2_service.api.location
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
