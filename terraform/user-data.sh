#!/bin/bash

# Update system packages
yum update -y

# Install basic packages
yum install -y \
    wget \
    curl \
    vim \
    git \
    htop \
    tree \
    unzip

# Install Docker
yum install -y docker
systemctl start docker
systemctl enable docker
usermod -a -G docker ec2-user

# Install Docker Compose
curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose

# Install Node.js (LTS version)
curl -sL https://rpm.nodesource.com/setup_lts.x | bash -
yum install -y nodejs

# Install .NET SDK (for your C# application)
rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
yum install -y dotnet-sdk-8.0

# Install AWS CLI v2
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
./aws/install
rm -rf awscliv2.zip aws/

# Set hostname
hostnamectl set-hostname ${hostname}
echo "127.0.0.1 ${hostname}" >> /etc/hosts

# Create application directory
mkdir -p /opt/cloud-calendar
chown ec2-user:ec2-user /opt/cloud-calendar

# Configure automatic security updates
yum install -y yum-cron
systemctl enable yum-cron
systemctl start yum-cron

# Create a simple nginx configuration for reverse proxy (optional)
yum install -y nginx
systemctl enable nginx

# Create a basic nginx config
cat > /etc/nginx/conf.d/app.conf << 'EOF'
server {
    listen 80;
    server_name _;
    
    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF

systemctl start nginx

# Log completion
echo "$(date): User data script completed" >> /var/log/user-data.log
