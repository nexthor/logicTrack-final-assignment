# EF Core One-to-Many Relationship Implementation

## Overview
This implementation establishes a proper one-to-many relationship between `Order` and `InventoryItem` entities using Entity Framework Core.

## Relationship Structure
- **One Order** can have **many InventoryItems**
- **One InventoryItem** can belong to **one Order** (or no order at all)

## Key Changes Made

### 1. InventoryItem Model Updates
- Added `OrderId` property as foreign key (nullable to allow standalone inventory items)
- Added `Order` navigation property to reference the parent order

### 2. Order Model Updates  
- Changed `List` property to `Items` (better naming convention)
- Updated `AddItem()` method to properly set foreign key and navigation properties
- Updated `RemoveItem()` method to clear foreign key relationship
- Updated `GetOrderSummary()` to use the renamed `Items` property

### 3. DbContext Configuration
- Added `OnModelCreating` method to configure the relationship using Fluent API
- Configured one-to-many relationship with `SetNull` delete behavior
- Added property constraints (max lengths for string properties)

### 4. Database Migration
- Created migration `AddOrderInventoryItemRelationship` to update database schema
- Applied migration to database successfully

## Key EF Core Concepts Demonstrated

### Navigation Properties
```csharp
// In Order class
public List<InventoryItem> Items { get; set; } = new List<InventoryItem>();

// In InventoryItem class  
public Order? Order { get; set; }
```

### Foreign Key Property
```csharp
// In InventoryItem class
public int? OrderId { get; set; }
```

### Fluent API Configuration
```csharp
modelBuilder.Entity<Order>()
    .HasMany(o => o.Items)
    .WithOne(i => i.Order)
    .HasForeignKey(i => i.OrderId)
    .OnDelete(DeleteBehavior.SetNull);
```

### Include() for Eager Loading
```csharp
var orderFromDb = context.Orders
    .Include(o => o.Items)
    .FirstOrDefault(o => o.CustomerName == "John Doe");
```

## Benefits of This Implementation

1. **Data Integrity**: Foreign key constraints ensure referential integrity
2. **Flexibility**: Items can exist without being assigned to orders
3. **Performance**: Proper navigation properties enable efficient querying
4. **Maintainability**: Clear relationship structure makes code easier to understand
5. **EF Core Features**: Full support for Include(), lazy loading, and change tracking

## Usage Examples

### Creating Orders with Items
```csharp
var order = new Order { CustomerName = "John Doe", DatePlaced = DateTime.Now };
var item = new InventoryItem { Name = "Laptop", Quantity = 1, Location = "Warehouse" };
order.AddItem(item);
context.Orders.Add(order);
context.SaveChanges();
```

### Querying with Related Data
```csharp
var orders = context.Orders.Include(o => o.Items).ToList();
var itemsWithOrders = context.InventoryItems.Include(i => i.Order).ToList();
```

### Managing Item Assignments
```csharp
order.RemoveItem(item); // Removes from order but keeps item in database
context.SaveChanges();
```

## Delete Behavior
- When an order is deleted, associated inventory items have their `OrderId` set to `null` (SetNull behavior)
- Items become standalone inventory items rather than being deleted
- This preserves inventory data while maintaining referential integrity