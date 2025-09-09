# Cloud Calendar - AWS EC2 Terraform Configuration

Dự án này sử dụng Terraform để tạo và quản lý EC2 instance trên AWS cho ứng dụng Cloud Calendar.

## Yêu cầu

- WSL (Windows Subsystem for Linux) đã được cài đặt và cấu hình
- [Terraform](https://www.terraform.io/downloads) >= 1.0 (cài trong WSL)
- [AWS CLI](https://aws.amazon.com/cli/) đã được cấu hình (trong WSL)
- SSH key pair đã được tạo (trong WSL)
- Quyền truy cập Internet từ WSL

## Cấu trúc file

```
terraform/
├── main.tf                 # Cấu hình chính
├── variables.tf           # Định nghĩa biến
├── outputs.tf            # Định nghĩa outputs
├── user-data.sh          # Script khởi tạo EC2
├── terraform.tfvars.example  # File cấu hình mẫu
└── README.md             # Hướng dẫn này
```

## Hướng dẫn sử dụng với WSL

### 0. Chuẩn bị WSL

#### Kiểm tra WSL đã được cài đặt
```bash
# Trong PowerShell hoặc Command Prompt
wsl --list --verbose
```

#### Nếu chưa có WSL, cài đặt Ubuntu
```bash
# Trong PowerShell (Admin)
wsl --install -d Ubuntu
```

#### Truy cập WSL
```bash
# Từ PowerShell hoặc Windows Terminal
wsl
```

### 1. Chuẩn bị

#### Tạo SSH Key Pair (nếu chưa có)
```bash
# Tạo thư mục .ssh nếu chưa có
mkdir -p ~/.ssh
chmod 700 ~/.ssh

# Tạo SSH key pair
ssh-keygen -t rsa -b 4096 -f ~/.ssh/cloud-calendar-key -N ""
chmod 600 ~/.ssh/cloud-calendar-key
chmod 644 ~/.ssh/cloud-calendar-key.pub
```

#### Cài đặt và cấu hình AWS CLI (trong WSL)
```bash
# Cài đặt AWS CLI v2 (nếu chưa có)
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install
rm -rf awscliv2.zip aws/

# Cấu hình AWS CLI
aws configure
```

### 2. Cấu hình Terraform

#### Sao chép file cấu hình mẫu
```bash
# Trong WSL, di chuyển đến thư mục terraform
cd /mnt/c/Users/huyho/OneDrive/Desktop/calendars/cloud_calendar/terraform

# Sao chép file cấu hình mẫu
cp terraform.tfvars.example terraform.tfvars
```

#### Chỉnh sửa file terraform.tfvars
```bash
# Sử dụng nano hoặc vim để chỉnh sửa
nano terraform.tfvars
# hoặc
vim terraform.tfvars

# Cập nhật đường dẫn public key cho WSL:
# public_key_path = "~/.ssh/cloud-calendar-key.pub"
```

### 3. Triển khai

#### Cài đặt Terraform trong WSL (nếu chưa có)
```bash
# Cập nhật package list
sudo apt update

# Cài đặt các dependencies
sudo apt install -y gnupg software-properties-common curl

# Thêm HashiCorp GPG key
wget -O- https://apt.releases.hashicorp.com/gpg | \
    gpg --dearmor | \
    sudo tee /usr/share/keyrings/hashicorp-archive-keyring.gpg

# Thêm HashiCorp repository
echo "deb [signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] \
    https://apt.releases.hashicorp.com $(lsb_release -cs) main" | \
    sudo tee /etc/apt/sources.list.d/hashicorp.list

# Cập nhật và cài đặt Terraform
sudo apt update
sudo apt install terraform
```

#### Khởi tạo Terraform
```bash
terraform init
```

#### Xem kế hoạch triển khai
```bash
terraform plan
```

#### Áp dụng cấu hình
```bash
terraform apply
```

### 4. Kết nối đến EC2

Sau khi triển khai thành công, sử dụng lệnh SSH được hiển thị trong output:
```bash
# Kết nối SSH từ WSL
ssh -i ~/.ssh/cloud-calendar-key ec2-user@<PUBLIC_IP>

# Hoặc nếu gặp lỗi permissions, chạy:
chmod 600 ~/.ssh/cloud-calendar-key
ssh -i ~/.ssh/cloud-calendar-key ec2-user@<PUBLIC_IP>
```

### 5. Dọn dẹp

Để xóa toàn bộ infrastructure:
```bash
terraform destroy
```

## Cấu hình mặc định

- **Instance Type**: t3.micro (Free Tier eligible)
- **OS**: Amazon Linux 2
- **Storage**: 20GB GP3 EBS (encrypted)
- **Security Group**: SSH (22), HTTP (80), HTTPS (443), Custom (8080)
- **Monitoring**: Enabled

## Packages được cài đặt tự động

- Docker & Docker Compose
- Node.js (LTS)
- .NET SDK 8.0
- AWS CLI v2
- Nginx (reverse proxy)
- Basic development tools (git, vim, htop, etc.)

## Biến cấu hình

| Biến | Mô tả | Mặc định |
|------|--------|----------|
| `aws_region` | AWS region | us-east-1 |
| `instance_type` | Loại EC2 instance | t3.micro |
| `instance_name` | Tên EC2 instance | cloud-calendar-server |
| `environment` | Môi trường (dev/staging/prod) | dev |
| `key_name` | Tên key pair | cloud-calendar-key |
| `public_key_path` | Đường dẫn public key | ~/.ssh/id_rsa.pub |
| `root_volume_size` | Kích thước ổ đĩa (GB) | 20 |
| `create_eip` | Tạo Elastic IP | false |

## Output

Sau khi triển khai, bạn sẽ nhận được:
- Instance ID
- Public IP
- Private IP
- Public DNS
- Security Group ID
- SSH connection command

## Lưu ý bảo mật

1. **SSH Key**: Bảo mật private key của bạn
2. **Security Group**: Chỉ mở các port cần thiết
3. **IAM**: Sử dụng IAM roles với quyền tối thiểu cần thiết
4. **Updates**: EC2 instance sẽ tự động cập nhật security patches

## Troubleshooting

### Lỗi thường gặp

1. **Key pair exists**: Nếu key pair đã tồn tại, xóa nó hoặc đổi tên
2. **Permission denied**: Kiểm tra quyền AWS CLI và IAM
3. **Instance limit**: Kiểm tra giới hạn EC2 trong region
4. **VPC not found**: Đảm bảo region có default VPC

### Logs

- User data logs: `/var/log/user-data.log`
- Cloud-init logs: `/var/log/cloud-init.log`
- System logs: `/var/log/messages`

## Tối ưu hóa chi phí

- Sử dụng `t3.micro` cho Free Tier
- Tắt instance khi không sử dụng
- Sử dụng Spot Instances cho môi trường dev/test
- Monitor usage với AWS Cost Explorer
