# Feature 014a — the send-payment-reminders CLI subcommand, run daily (research.md R4). This is
# this codebase's first scheduled-job infrastructure: a Cloud Run Job execution of the same
# container image the api service runs, triggered by Cloud Scheduler via the Cloud Run Admin
# API's `run` endpoint (OAuth-authenticated as the same default compute service account already
# used elsewhere in this file — no new identity).
#
# NOTE: authoring only. Applying this (terraform apply) against the real GCP project is a
# manual, explicit post-merge step — this loop never runs `terraform apply` autonomously, same
# convention as every other production/infra change in this codebase.

resource "google_project_service" "cloudscheduler" {
  service            = "cloudscheduler.googleapis.com"
  disable_on_destroy = false
}

resource "google_cloud_run_v2_job" "send_payment_reminders" {
  name     = "${var.service_name}-send-payment-reminders"
  location = var.region

  template {
    template {
      containers {
        image = var.image
        args  = ["send-payment-reminders"]

        env {
          name  = "ConnectionStrings__DefaultConnection"
          value = var.db_connection_string
        }
        env {
          name  = "Mollie__ClientId"
          value = var.mollie_client_id
        }
        env {
          name  = "Mollie__ClientSecret"
          value = var.mollie_client_secret
        }
        env {
          name  = "Mollie__RedirectUri"
          value = var.mollie_redirect_uri
        }
        env {
          name  = "App__ApiBaseUrl"
          value = var.app_api_base_url
        }
      }

      # A CLI subcommand run — never retry automatically. SendPaymentRemindersCommand.RunAsync
      # already isolates per-tenant failures internally (research.md R4); a retried whole-job
      # execution would risk double-sending reminders for tenants that already succeeded.
      max_retries = 0
    }
  }

  depends_on = [google_artifact_registry_repository.api]
}

resource "google_cloud_scheduler_job" "send_payment_reminders_daily" {
  name      = "${var.service_name}-send-payment-reminders-daily"
  region    = var.region
  schedule  = "0 6 * * *" # 06:00 daily — ahead of typical business hours, per location timezone assumptions in spec.md
  time_zone = "Europe/Brussels"

  http_target {
    http_method = "POST"
    uri         = "https://${var.region}-run.googleapis.com/apis/run.googleapis.com/v1/namespaces/${var.project_id}/jobs/${google_cloud_run_v2_job.send_payment_reminders.name}:run"

    oauth_token {
      service_account_email = data.google_compute_default_service_account.default.email
    }
  }

  depends_on = [google_project_service.cloudscheduler]
}
