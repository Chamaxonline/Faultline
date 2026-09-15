resource "hcloud_ssh_key" "deploy" {
  name       = "${var.server_name}-deploy"
  public_key = file(var.ssh_public_key_path)
}

resource "hcloud_firewall" "faultline" {
  name = "${var.server_name}-firewall"

  rule {
    direction  = "in"
    protocol   = "tcp"
    port       = "22"
    source_ips = var.admin_ip_cidrs
  }

  rule {
    direction  = "in"
    protocol   = "tcp"
    port       = "80"
    source_ips = ["0.0.0.0/0", "::/0"]
  }

  rule {
    direction  = "in"
    protocol   = "tcp"
    port       = "443"
    source_ips = ["0.0.0.0/0", "::/0"]
  }
}

resource "hcloud_server" "faultline" {
  name        = var.server_name
  server_type = var.server_type
  location    = var.location
  image       = "ubuntu-22.04"

  ssh_keys     = [hcloud_ssh_key.deploy.id]
  firewall_ids = [hcloud_firewall.faultline.id]
  user_data    = file("${path.module}/cloud-init.yml")

  labels = {
    project = "faultline"
  }
}
