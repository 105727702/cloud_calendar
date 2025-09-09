# Get the default VPC and subnet
data "aws_vpc" "default" {
  default = true
}

data "aws_subnet" "default" {
  vpc_id            = data.aws_vpc.default.id
  availability_zone = var.availability_zone
  default_for_az    = true
}

# Get the latest Amazon Linux 2 AMI
data "aws_ami" "amazon_linux" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["amzn2-ami-hvm-*-x86_64-gp2"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

# Create security group for EC2 instance
resource "aws_security_group" "main" {
  name_prefix = "cloud-calendar-sg-"
  description = "Security group for Cloud Calendar application"
  vpc_id      = data.aws_vpc.default.id

  # SSH access
  ingress {
    description = "SSH"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # HTTP access
  ingress {
    description = "HTTP"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # HTTPS access
  ingress {
    description = "HTTPS"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # Application port
  ingress {
    description = "App Port"
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # All outbound traffic
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "Cloud Calendar Security Group"
    Environment = var.environment
    Project     = "CloudCalendar"
  }
}

# Create key pair for EC2 instance
resource "aws_key_pair" "main" {
  key_name   = var.key_name
  public_key = file(var.public_key_path)

  tags = {
    Name        = "Cloud Calendar Key Pair"
    Environment = var.environment
    Project     = "CloudCalendar"
  }
}

# Create EC2 instance
resource "aws_instance" "main" {
  ami                    = data.aws_ami.amazon_linux.id
  instance_type          = var.instance_type
  key_name              = aws_key_pair.main.key_name
  vpc_security_group_ids = [aws_security_group.main.id]
  subnet_id             = data.aws_subnet.default.id

  # Enable detailed monitoring
  monitoring = true

  # Root block device configuration
  root_block_device {
    volume_type           = "gp3"
    volume_size          = var.root_volume_size
    encrypted            = true
    delete_on_termination = true
    tags = {
      Name        = "Cloud Calendar Root Volume"
      Environment = var.environment
    }
  }

  # User data script for automatic setup
  user_data = base64encode(templatefile("${path.root}/user-data.sh", {
    hostname = var.instance_name
  }))

  tags = {
    Name        = var.instance_name
    Environment = var.environment
    Project     = "CloudCalendar"
  }
}

# Create Elastic IP (optional)
resource "aws_eip" "main" {
  count    = var.create_eip ? 1 : 0
  instance = aws_instance.main.id
  domain   = "vpc"

  tags = {
    Name        = "${var.instance_name}-eip"
    Environment = var.environment
    Project     = "CloudCalendar"
  }

  depends_on = [aws_instance.main]
}
