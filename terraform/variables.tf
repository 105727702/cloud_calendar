variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "availability_zone" {
  description = "Availability zone for the EC2 instance"
  type        = string
  default     = "us-east-1a"
}

variable "instance_type" {
  description = "EC2 instance type"
  type        = string
  default     = "t3.micro"
  
  validation {
    condition = contains([
      "t2.nano", "t2.micro", "t2.small", "t2.medium", "t2.large",
      "t3.nano", "t3.micro", "t3.small", "t3.medium", "t3.large",
      "t3.xlarge", "t3.2xlarge"
    ], var.instance_type)
    error_message = "Instance type must be a valid EC2 instance type."
  }
}

variable "instance_name" {
  description = "Name for the EC2 instance"
  type        = string
  default     = "cloud-calendar-server"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
  
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be dev, staging, or prod."
  }
}

variable "key_name" {
  description = "Name for the AWS key pair"
  type        = string
  default     = "cloud-calendar-key"
}

variable "public_key_path" {
  description = "Path to the public key file"
  type        = string
  default     = "cloud-calendar-key.pub"
}

variable "root_volume_size" {
  description = "Size of the root EBS volume in GB"
  type        = number
  default     = 20
  
  validation {
    condition     = var.root_volume_size >= 8 && var.root_volume_size <= 100
    error_message = "Root volume size must be between 8 and 100 GB."
  }
}

variable "create_eip" {
  description = "Whether to create and associate an Elastic IP"
  type        = bool
  default     = false
}
