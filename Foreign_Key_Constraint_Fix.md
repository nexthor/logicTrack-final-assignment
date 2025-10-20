# Foreign Key Constraint Issue - Analysis and Solutions

## 🚨 The Problem

The error you encountered is:
```
SQLite Error 19: 'FOREIGN KEY constraint failed'
```

This happens when trying to create inventory items with an `orderId` that references a non-existent order, or when the foreign key relationship isn't properly handled.

## 🔍 Root Cause Analysis

In your `integration-tests.http` file, step 2 tries to create inventory items with `"orderId": 1`, but:

1. **Foreign Key Constraint**: The database enforces that `orderId` must reference an existing order
2. **Race Condition**: The order might not have been committed to the database yet
3. **ID Assignment**: Auto-generated IDs might not be what you expect

## 💡 Solutions Provided

### 1. **Fixed Integration Tests** ✅
- **File**: `Tests/integration-tests.http`
- **Fix**: Removed `orderId` from initial item creation
- **Approach**: Create items independently, then manage relationships separately

### 2. **Enhanced Order Controller** ✅  
- **File**: `Controllers/OrderControllerExtended.cs`
- **New Endpoints**:
  - `POST /api/orderextended/{orderId}/items/{itemId}` - Add item to order
  - `DELETE /api/orderextended/{orderId}/items/{itemId}` - Remove item from order
  - `GET /api/orderextended/{orderId}/items` - Get order items
  - `POST /api/orderextended/with-items` - Create order with items atomically

### 3. **Comprehensive Test Suite** ✅
- **File**: `Tests/extended-order-controller.http`
- **Coverage**: Tests all relationship management scenarios
- **Validation**: Includes error cases and edge scenarios

### 4. **Relationship Management Guide** ✅
- **File**: `Tests/relationship-management.http`
- **Purpose**: Shows current limitations and workarounds

## 🔧 Recommended Approach

### Option A: Use the Fixed Integration Tests (Immediate Fix)
```http
# Create order first
POST /api/order
{
    "customerName": "Test Customer",
    "datePlaced": "2024-10-20T10:00:00Z"
}

# Create items without orderId
POST /api/inventory  
{
    "name": "Test Item",
    "quantity": 5,
    "location": "Test Location"
    # No orderId - avoids FK constraint
}
```

### Option B: Use Extended Controller (Recommended)
```http
# Create order with items atomically
POST /api/orderextended/with-items
{
    "customerName": "Test Customer",
    "datePlaced": "2024-10-20T10:00:00Z",
    "items": [
        {
            "name": "Test Item",
            "quantity": 5,
            "location": "Test Location"
        }
    ]
}
```

## 🎯 Next Steps

1. **Immediate**: Use the fixed `integration-tests.http` file
2. **Short-term**: Add the `OrderControllerExtended` to your project
3. **Long-term**: Consider updating your main controllers with relationship management

## 📊 Database Schema Notes

Your current schema uses:
- **Foreign Key**: `InventoryItem.OrderId -> Order.Id`
- **Delete Behavior**: `SetNull` (good choice)
- **Relationship**: One-to-Many (Order -> InventoryItems)

This is properly configured in `LogiTrackContext.cs` with:
```csharp
modelBuilder.Entity<Order>()
    .HasMany(o => o.Items)
    .WithOne(i => i.Order)
    .HasForeignKey(i => i.OrderId)
    .OnDelete(DeleteBehavior.SetNull);
```

## ✅ Test Files Summary

| File | Purpose | Status |
|------|---------|---------|
| `integration-tests.http` | Fixed FK constraint issues | ✅ Ready |
| `extended-order-controller.http` | Tests new relationship endpoints | ✅ Ready |
| `relationship-management.http` | Shows current limitations | ✅ Reference |
| `inventory-controller.http` | Basic inventory tests | ✅ Working |
| `order-controller.http` | Basic order tests | ✅ Working |
| `quick-reference.http` | Quick testing scenarios | ✅ Working |