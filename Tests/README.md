# LogiTrack API Testing Quick Reference

## Overview
This directory contains comprehensive HTTP test files for the LogiTrack API. Use these files with the REST Client extension in VS Code.

## Test Files

### 1. `auth-controller.http`
Tests authentication endpoints:
- User registration
- User login  
- Role assignment
- Input validation
- Error handling

### 2. `inventory-controller-enhanced.http`
Tests inventory management with authorization:
- CRUD operations with proper authorization
- Pagination
- Input validation
- Role-based access control

### 3. `integration-test-suite.http`
Complete end-to-end testing workflow:
- User registration → Role assignment → Inventory management
- Tests the complete user journey
- Comprehensive error scenario testing

### 4. `inventory-controller.http` (Legacy)
Original inventory tests - still functional but less comprehensive

## Quick Start

### Prerequisites
1. Start the LogiTrack API server (usually `dotnet run`)
2. Ensure the base URL is correct in test files (default: `http://localhost:5205`)

### Running Tests

#### Option 1: Complete Integration Test
1. Open `integration-test-suite.http`
2. Run tests sequentially from top to bottom
3. This will create users, assign roles, and test all functionality

#### Option 2: Individual Component Testing
1. **Authentication**: Use `auth-controller.http`
2. **Inventory**: Use `inventory-controller-enhanced.http`
   - First run auth tests to get tokens
   - Copy tokens to inventory tests

## Test User Accounts

### Default Test Users (for integration tests):
- **Admin**: admin@logitrack.com / AdminPass123!
- **Manager**: manager@logitrack.com / ManagerPass123!  
- **User**: user@logitrack.com / UserPass123!

### Roles and Permissions:
- **User**: Can read inventory items only
- **Manager**: Can read and modify inventory items
- **Admin**: Full access to all operations

## Common Scenarios

### Get JWT Token
```http
POST http://localhost:5205/api/auth/login
Content-Type: application/json

{
    "email": "manager@logitrack.com",
    "password": "ManagerPass123!"
}
```

### Use Token in Requests
```http
GET http://localhost:5205/api/inventory
Authorization: Bearer YOUR_TOKEN_HERE
```

### Create Inventory Item
```http
POST http://localhost:5205/api/inventory
Authorization: Bearer MANAGER_TOKEN
Content-Type: application/json

{
    "name": "Test Item",
    "quantity": 5,
    "location": "Warehouse A"
}
```

## API Endpoints Summary

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login user
- `POST /api/auth/assign-role` - Assign role to user

### Inventory  
- `GET /api/inventory` - Get all items (with pagination)
- `GET /api/inventory/{id}` - Get specific item
- `POST /api/inventory` - Create item (Manager+)
- `PUT /api/inventory/{id}` - Update item (Manager+)
- `DELETE /api/inventory/{id}` - Delete item (Manager+)

## Troubleshooting

### Common Issues:
1. **401 Unauthorized**: Check if token is valid and included in Authorization header
2. **403 Forbidden**: User doesn't have required role for the operation
3. **400 Bad Request**: Check request body format and validation rules
4. **404 Not Found**: Item ID doesn't exist

### Token Issues:
- Tokens expire based on JWT configuration
- Re-login to get fresh token if expired
- Ensure Bearer prefix in Authorization header

### Role Issues:
- New users have no roles by default
- Use assign-role endpoint to grant permissions
- Manager role required for inventory modifications

## Validation Rules

### Inventory Items:
- **Name**: Required, max 100 characters
- **Quantity**: Required, non-negative integer
- **Location**: Optional, max 200 characters

### User Registration:
- **Email**: Required, valid email format
- **Password**: Required, meets password policy

## Response Formats

### Success Response:
```json
{
    "data": [...],
    "pagination": {
        "currentPage": 1,
        "pageSize": 10,
        "totalItems": 25,
        "totalPages": 3
    }
}
```

### Error Response:
```json
{
    "message": "Error description",
    "errors": ["Detailed error 1", "Detailed error 2"]
}
```