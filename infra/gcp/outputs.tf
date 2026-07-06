output "service_url" {
  description = "Public URL of the Cloud Run service"
  value       = google_cloud_run_v2_service.api.uri
}

output "artifact_registry_repo" {
  description = "Docker registry prefix to use in image tags"
  value       = "${var.region}-docker.pkg.dev/${var.project_id}/childcare"
}

output "workload_identity_provider" {
  description = "Workload Identity Provider — use as GCP_WORKLOAD_IDENTITY_PROVIDER GitHub secret"
  value       = google_iam_workload_identity_pool_provider.github_actions.name
}

output "service_account" {
  description = "GitHub Actions service account email — use as GCP_SERVICE_ACCOUNT GitHub secret"
  value       = google_service_account.github_actions.email
}

output "staff_photos_bucket_name" {
  description = "GCS bucket name for staff profile photos (feature 005-staff)"
  value       = google_storage_bucket.staff_profile_photos.name
}
