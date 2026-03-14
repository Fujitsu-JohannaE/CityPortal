# ─── Shared Storage Account (all tenants, attachments) ───────────────────────
#
# This is a SINGLE storage account shared across all tenants.
# Tenant isolation is achieved via blob path prefix: {tenantSlug}/{formSlug}/...
# Microsoft Defender for Storage scans uploads for malware.

resource "azurerm_storage_account" "attachments" {
  name                     = "st${var.project}files${var.environment}"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "StorageV2"

  # Security hardening
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  https_traffic_only_enabled      = true

  blob_properties {
    # Soft delete for accidental deletion recovery
    delete_retention_policy {
      days = 7
    }
    container_delete_retention_policy {
      days = 7
    }
  }

  tags = local.tags
}

# Container for form attachments (private — no public access)
resource "azurerm_storage_container" "form_attachments" {
  name                  = "form-attachments"
  storage_account_id    = azurerm_storage_account.attachments.id
  container_access_type = "private"
}

# ─── Microsoft Defender for Storage (malware scanning) ───────────────────────

resource "azurerm_security_center_storage_defender" "main" {
  storage_account_id                          = azurerm_storage_account.attachments.id
  malware_scanning_on_upload_enabled          = true
  malware_scanning_on_upload_cap_gb_per_month = 5 # GB per month — adjust for production
  sensitive_data_discovery_enabled            = false

  override_subscription_settings_enabled = true
}

# ─── RBAC: App Service → Storage ─────────────────────────────────────────────

# Read/write blobs (upload + download attachments)
resource "azurerm_role_assignment" "app_blob_contributor" {
  scope                = azurerm_storage_account.attachments.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_web_app.main.identity[0].principal_id
}

# Read/write blob index tags (Defender scan results are written as tags)
resource "azurerm_role_assignment" "app_blob_tag_owner" {
  scope                = azurerm_storage_account.attachments.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_linux_web_app.main.identity[0].principal_id
}
