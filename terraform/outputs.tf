# Infrastructure outputs
output "vpc_id" {
  description = "ID of the VPC"
  value       = module.infrastructure.vpc_id
}

output "subnet_id" {
  description = "ID of the subnet"
  value       = module.infrastructure.subnet_id
}

output "security_group_id" {
  description = "ID of the security group"
  value       = module.infrastructure.security_group_id
}

# Instance outputs
output "instance_id" {
  description = "ID of the EC2 instance"
  value       = module.infrastructure.instance_id
}

output "instance_public_ip" {
  description = "Public IP address of the EC2 instance"
  value       = module.infrastructure.instance_public_ip
}

output "instance_private_ip" {
  description = "Private IP address of the EC2 instance"
  value       = module.infrastructure.instance_private_ip
}

output "instance_public_dns" {
  description = "Public DNS name of the EC2 instance"
  value       = module.infrastructure.instance_public_dns
}

output "instance_state" {
  description = "State of the EC2 instance"
  value       = module.infrastructure.instance_state
}

output "elastic_ip" {
  description = "Elastic IP address (if created)"
  value       = module.infrastructure.elastic_ip
}

output "ssh_connection" {
  description = "SSH connection command"
  value       = module.infrastructure.ssh_connection
}
