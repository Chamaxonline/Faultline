variable "hcloud_token" {
  description = "Hetzner Cloud API token. Set via TF_VAR_hcloud_token env var — never commit this."
  type        = string
  sensitive   = true
}

variable "server_name" {
  description = "Name of the Faultline server."
  type        = string
  default     = "faultline"
}

variable "server_type" {
  description = "Hetzner server type. CPX31 per ADR-0001's cost estimate (4 vCPU / 8GB) runs the whole stack — Postgres, Redis, Api, Worker, dashboard, Caddy."
  type        = string
  default     = "cpx31"
}

variable "location" {
  description = "Hetzner datacenter location."
  type        = string
  default     = "nbg1" # Nuremberg — closest to Bistec's usual EU footprint; change if your team is elsewhere
}

variable "ssh_public_key_path" {
  description = "Path to the SSH public key that can log into the server."
  type        = string
  default     = "~/.ssh/id_ed25519.pub"
}

variable "admin_ip_cidrs" {
  description = "CIDR blocks allowed to SSH in (port 22). Keep this tight — e.g. your office/VPN egress IP."
  type        = list(string)
}
