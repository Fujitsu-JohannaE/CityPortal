# ─── Project ──────────────────────────────────────────────────────────────────

variable "project" {
  description = "Project name, used in resource naming"
  type        = string
  default     = "cityportal"
}

variable "environment" {
  description = "Environment (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "northeurope"
}

variable "subscription_id" {
  description = "Azure subscription ID"
  type        = string
}

# ─── SQL ──────────────────────────────────────────────────────────────────────

variable "sql_admin_login" {
  description = "SQL Server admin username"
  type        = string
  default     = "sqladmin"
}

variable "sql_admin_password" {
  description = "SQL Server admin password"
  type        = string
  sensitive   = true
}

# ─── App Service ──────────────────────────────────────────────────────────────

variable "app_service_sku" {
  description = "App Service Plan SKU"
  type        = string
  default     = "B1" # Basic tier — cheapest for demo with Managed Identity support
}
