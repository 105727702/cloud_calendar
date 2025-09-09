# Configure the AWS Provider
provider "aws" {
  region = var.aws_region
}

# Cloud Calendar Infrastructure Module
module "infrastructure" {
  source = "./modules/infrastructure"
  
  # Pass all variables to the module
  availability_zone  = var.availability_zone
  instance_type     = var.instance_type
  instance_name     = var.instance_name
  environment       = var.environment
  key_name          = var.key_name
  public_key_path   = var.public_key_path
  root_volume_size  = var.root_volume_size
  create_eip        = var.create_eip
}
