# Hướng dẫn bảo mật Database Configuration

## Đã thực hiện:

### 1. Tạo file cấu hình
- ✅ `appsettings.json` - Cấu hình mặc định (không chứa password thực)
- ✅ `appsettings.local.json` - Cấu hình local với password thực
- ✅ `.gitignore` - Bảo vệ file sensitive

### 2. Cấu hình bảo mật
- ✅ Password được đọc từ Environment Variables hoặc file local
- ✅ File `appsettings.local.json` sẽ không được commit lên Git
- ✅ Hỗ trợ nhiều môi trường (Dev, Production)

## Cách sử dụng:

### Option 1: Sử dụng appsettings.local.json (Khuyến nghị cho development)
File `appsettings.local.json` đã được tạo với password thực của bạn.

### Option 2: Sử dụng Environment Variables (Khuyến nghị cho production)
```bash
# Windows
set DATABASE_MYSQL_PASSWORD=your_password_here
set DATABASE_MYSQL_SERVER=your_server_here

# Linux/Mac
export DATABASE_MYSQL_PASSWORD=your_password_here
export DATABASE_MYSQL_SERVER=your_server_here
```

### Option 3: Cấu hình qua appsettings.json
Chỉnh sửa `appsettings.json` với thông tin không sensitive.

## Priority của cấu hình:
1. Environment Variables (cao nhất)
2. appsettings.local.json
3. appsettings.json (thấp nhất)

## Production Deployment:
1. Không deploy file `appsettings.local.json`
2. Sử dụng Environment Variables hoặc Azure Key Vault
3. Đảm bảo password không hardcode trong source code

## File được bảo vệ bởi .gitignore:
- `appsettings.local.json`
- `appsettings.Production.json`
- `appsettings.Development.json`
- `.env*`
- `*.db`
- `logs/`

## Lưu ý bảo mật:
⚠️ KHÔNG BAO GIỜ commit password thực vào Git repository
⚠️ Sử dụng strong password cho database
⚠️ Định kỳ thay đổi password
⚠️ Giới hạn quyền truy cập database
