using Microsoft.Data.Sqlite;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SatisTakip;

public sealed class DatabaseService
{
    private const string DefaultAdminPassword = "051575045";
    private readonly string connectionString;
    public string DatabasePath { get; }

    public DatabaseService()
    {
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SatisTakip");
        Directory.CreateDirectory(dataDirectory);
        DatabasePath = Path.Combine(dataDirectory, "satis_takip.db");
        connectionString = $"Data Source={DatabasePath}";
        Initialize();
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Barcode TEXT NOT NULL DEFAULT '',
                Name TEXT NOT NULL,
                Category TEXT NOT NULL DEFAULT 'Genel',
                CostPrice REAL NOT NULL DEFAULT 0,
                VatRate REAL NULL,
                Price REAL NOT NULL DEFAULT 0,
                Stock REAL NOT NULL DEFAULT 0,
                MinimumStock REAL NOT NULL DEFAULT 5,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Products_Barcode ON Products(Barcode) WHERE Barcode <> '';
            CREATE TABLE IF NOT EXISTS Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL COLLATE NOCASE UNIQUE
            );
            CREATE TABLE IF NOT EXISTS Sales (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleDate TEXT NOT NULL,
                Revenue REAL NOT NULL,
                Cost REAL NOT NULL,
                PaymentMethod TEXT NOT NULL,
                CustomerId INTEGER NULL,
                IsRefunded INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS SaleItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity REAL NOT NULL,
                ReturnedQuantity REAL NOT NULL DEFAULT 0,
                UnitPrice REAL NOT NULL,
                UnitCost REAL NOT NULL,
                FOREIGN KEY(SaleId) REFERENCES Sales(Id)
            );
            CREATE TABLE IF NOT EXISTS Expenses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ExpenseDate TEXT NOT NULL,
                Description TEXT NOT NULL,
                Amount REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Phone TEXT NOT NULL DEFAULT '',
                Balance REAL NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS Payments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                PaymentDate TEXT NOT NULL,
                Amount REAL NOT NULL,
                Note TEXT NOT NULL DEFAULT '',
                FOREIGN KEY(CustomerId) REFERENCES Customers(Id)
            );
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'Cashier',
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS Settings (
                [Key] TEXT PRIMARY KEY,
                [Value] TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS StockMovements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                MovementDate TEXT NOT NULL,
                MovementType TEXT NOT NULL,
                Quantity REAL NOT NULL,
                PreviousStock REAL NOT NULL,
                NewStock REAL NOT NULL,
                Note TEXT NOT NULL DEFAULT '',
                Username TEXT NOT NULL DEFAULT '',
                FOREIGN KEY(ProductId) REFERENCES Products(Id)
            );
            """;
        command.ExecuteNonQuery();
        EnsureCategories();
        EnsureProductMinimumStockColumn();
        EnsureProductVatRateColumn();
        EnsureSalesPaymentColumns();
        EnsureSaleItemReturnColumn();
        EnsureDefaultUser();
    }

    private void EnsureCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Products SET Category = 'Genel' WHERE TRIM(Category) = '';
            INSERT OR IGNORE INTO Categories (Name)
            SELECT DISTINCT TRIM(Category) FROM Products WHERE TRIM(Category) <> '';
            """;
        command.ExecuteNonQuery();
    }

    private void EnsureProductMinimumStockColumn()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Products)";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "MinimumStock")
            {
                return;
            }
        }

        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE Products ADD COLUMN MinimumStock REAL NOT NULL DEFAULT 5";
        alter.ExecuteNonQuery();
    }

    private void EnsureProductVatRateColumn()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Products)";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "VatRate")
            {
                return;
            }
        }

        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE Products ADD COLUMN VatRate REAL NULL";
        alter.ExecuteNonQuery();
    }

    private void EnsureSalesPaymentColumns()
    {
        using var connection = OpenConnection();
        foreach (var column in new[] { "CashAmount", "CardAmount" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE Sales ADD COLUMN {column} REAL NOT NULL DEFAULT 0";
            try { command.ExecuteNonQuery(); } catch (SqliteException exception) when (exception.SqliteErrorCode == 1) { }
        }
    }

    private void EnsureSaleItemReturnColumn()
    {
        using var connection = OpenConnection();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(SaleItems)";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1) == "ReturnedQuantity")
                {
                    return;
                }
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "ALTER TABLE SaleItems ADD COLUMN ReturnedQuantity REAL NOT NULL DEFAULT 0";
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE SaleItems
                SET ReturnedQuantity = Quantity
                WHERE EXISTS (
                    SELECT 1 FROM Sales
                    WHERE Sales.Id = SaleItems.SaleId AND Sales.IsRefunded = 1
                )
                """;
            command.ExecuteNonQuery();
        }
    }

    public List<Product> LoadProducts()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Barcode, Name, Category, CostPrice, VatRate, Price, Stock, MinimumStock FROM Products WHERE IsActive = 1 ORDER BY Name";
        using var reader = command.ExecuteReader();
        var products = new List<Product>();
        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt64(0),
                Barcode = reader.GetString(1),
                Name = reader.GetString(2),
                Category = reader.GetString(3),
                CostPrice = ReadDecimal(reader, 4),
                VatRate = reader.IsDBNull(5) ? null : ReadDecimal(reader, 5),
                Price = ReadDecimal(reader, 6),
                Stock = ReadDecimal(reader, 7),
                MinimumStock = ReadDecimal(reader, 8)
            });
        }
        return products;
    }

    public List<string> LoadCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Categories ORDER BY Name COLLATE NOCASE";
        using var reader = command.ExecuteReader();
        var categories = new List<string>();
        while (reader.Read())
        {
            categories.Add(reader.GetString(0));
        }
        return categories;
    }

    public void SaveCategory(string name)
    {
        var category = name.Trim();
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Kategori adı boş olamaz.", nameof(name));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Categories (Name) VALUES ($name)";
        command.Parameters.AddWithValue("$name", category);
        command.ExecuteNonQuery();
    }

    public List<string> LoadProductNamesInCategory(string categoryName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Products WHERE Category = $category COLLATE NOCASE ORDER BY Name COLLATE NOCASE";
        command.Parameters.AddWithValue("$category", categoryName.Trim());
        using var reader = command.ExecuteReader();
        var productNames = new List<string>();
        while (reader.Read())
        {
            productNames.Add(reader.GetString(0));
        }
        return productNames;
    }

    public void RenameCategory(string oldName, string newName)
    {
        var oldCategory = oldName.Trim();
        var newCategory = newName.Trim();
        if (string.IsNullOrWhiteSpace(oldCategory) || string.IsNullOrWhiteSpace(newCategory))
        {
            throw new ArgumentException("Kategori adı boş olamaz.");
        }
        if (string.Equals(oldCategory, "Genel", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Genel kategorisi varsayılan kategori olduğu için yeniden adlandırılamaz.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT EXISTS(SELECT 1 FROM Categories WHERE Name = $name COLLATE NOCASE)";
            find.Parameters.AddWithValue("$name", oldCategory);
            if (Convert.ToInt64(find.ExecuteScalar()) != 1)
            {
                throw new InvalidOperationException("Düzenlenecek kategori bulunamadı.");
            }
        }

        using (var duplicate = connection.CreateCommand())
        {
            duplicate.Transaction = transaction;
            duplicate.CommandText = "SELECT EXISTS(SELECT 1 FROM Categories WHERE Name = $newName COLLATE NOCASE AND Name <> $oldName COLLATE NOCASE)";
            duplicate.Parameters.AddWithValue("$newName", newCategory);
            duplicate.Parameters.AddWithValue("$oldName", oldCategory);
            if (Convert.ToInt64(duplicate.ExecuteScalar()) == 1)
            {
                throw new InvalidOperationException("Bu isimde bir kategori zaten var.");
            }
        }

        using (var updateProducts = connection.CreateCommand())
        {
            updateProducts.Transaction = transaction;
            updateProducts.CommandText = "UPDATE Products SET Category = $newName WHERE Category = $oldName COLLATE NOCASE";
            updateProducts.Parameters.AddWithValue("$newName", newCategory);
            updateProducts.Parameters.AddWithValue("$oldName", oldCategory);
            updateProducts.ExecuteNonQuery();
        }

        using (var updateCategory = connection.CreateCommand())
        {
            updateCategory.Transaction = transaction;
            updateCategory.CommandText = "UPDATE Categories SET Name = $newName WHERE Name = $oldName COLLATE NOCASE";
            updateCategory.Parameters.AddWithValue("$newName", newCategory);
            updateCategory.Parameters.AddWithValue("$oldName", oldCategory);
            updateCategory.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void DeleteCategory(string name)
    {
        var category = name.Trim();
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Kategori adı boş olamaz.", nameof(name));
        }
        if (string.Equals(category, "Genel", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Genel kategorisi varsayılan kategori olduğu için silinemez.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT EXISTS(SELECT 1 FROM Categories WHERE Name = $name COLLATE NOCASE)";
            find.Parameters.AddWithValue("$name", category);
            if (Convert.ToInt64(find.ExecuteScalar()) != 1)
            {
                throw new InvalidOperationException("Silinecek kategori bulunamadı.");
            }
        }

        long productCount;
        using (var count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM Products WHERE Category = $name COLLATE NOCASE";
            count.Parameters.AddWithValue("$name", category);
            productCount = Convert.ToInt64(count.ExecuteScalar());
        }

        if (productCount > 0)
        {
            EnsureCategoryExists(connection, transaction, "Genel");
            using var moveProducts = connection.CreateCommand();
            moveProducts.Transaction = transaction;
            moveProducts.CommandText = "UPDATE Products SET Category = 'Genel' WHERE Category = $name COLLATE NOCASE";
            moveProducts.Parameters.AddWithValue("$name", category);
            moveProducts.ExecuteNonQuery();
        }

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Categories WHERE Name = $name COLLATE NOCASE";
            delete.Parameters.AddWithValue("$name", category);
            delete.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void SaveProduct(Product product)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        product.Category = string.IsNullOrWhiteSpace(product.Category) ? "Genel" : product.Category.Trim();
        EnsureCategoryExists(connection, transaction, product.Category);
        long existingId = 0;
        decimal previousStock = 0;
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = product.Id > 0
                ? "SELECT Id, Stock FROM Products WHERE Id = $id"
                : """
                  SELECT Id, Stock FROM Products
                  WHERE (Barcode = $barcode AND $barcode <> '')
                     OR (Barcode = '' AND Name = $name AND IsActive = 1)
                  ORDER BY IsActive DESC
                  LIMIT 1
                  """;
            if (product.Id > 0)
            {
                find.Parameters.AddWithValue("$id", product.Id);
            }
            else
            {
                find.Parameters.AddWithValue("$barcode", product.Barcode);
                find.Parameters.AddWithValue("$name", product.Name);
            }
            using var reader = find.ExecuteReader();
            if (reader.Read())
            {
                existingId = reader.GetInt64(0);
                previousStock = ReadDecimal(reader, 1);
            }
        }

        using var save = connection.CreateCommand();
        save.Transaction = transaction;
        save.CommandText = existingId > 0
            ? """
              UPDATE Products
              SET Barcode = $barcode, Name = $name, Category = $category,
                  CostPrice = $cost, VatRate = $vatRate, Price = $price, Stock = $stock,
                  MinimumStock = $minimumStock, IsActive = 1
              WHERE Id = $id
              """
            : """
              INSERT INTO Products (Barcode, Name, Category, CostPrice, VatRate, Price, Stock, MinimumStock)
              VALUES ($barcode, $name, $category, $cost, $vatRate, $price, $stock, $minimumStock)
              """;
        AddProductParameters(save, product);
        if (existingId > 0)
        {
            save.Parameters.AddWithValue("$id", existingId);
            save.ExecuteNonQuery();
            product.Id = existingId;
        }
        else
        {
            save.ExecuteNonQuery();
            product.Id = Convert.ToInt64(GetLastInsertId(connection, transaction), CultureInfo.InvariantCulture);
        }

        if (existingId > 0 && previousStock != product.Stock)
        {
            RecordStockMovement(connection, transaction, product.Id, product.Name, "Stok düzeltme", product.Stock - previousStock, previousStock, product.Stock, "Ürün kaydından yapılan stok değişikliği");
        }
        else if (existingId == 0 && product.Stock > 0)
        {
            RecordStockMovement(connection, transaction, product.Id, product.Name, "İlk stok", product.Stock, 0, product.Stock, "Ürün ilk kez kaydedildi");
        }
        transaction.Commit();
    }

    public bool IsBarcodeInUse(string barcode, string? currentBarcode = null)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return false;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Products WHERE Barcode = $barcode AND IsActive = 1 AND ($currentBarcode IS NULL OR Barcode <> $currentBarcode))";
        command.Parameters.AddWithValue("$barcode", barcode.Trim());
        command.Parameters.AddWithValue("$currentBarcode", (object?)currentBarcode ?? DBNull.Value);
        return Convert.ToInt64(command.ExecuteScalar()) == 1;
    }

    public string GenerateNextInternalBarcode()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Barcode FROM Products WHERE Barcode LIKE 'STK%'";
        using var reader = command.ExecuteReader();
        var existingBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxSequence = 0;
        while (reader.Read())
        {
            var barcode = reader.GetString(0);
            existingBarcodes.Add(barcode);
            if (barcode.Length > 3 && int.TryParse(barcode.AsSpan(3), out var sequence))
            {
                maxSequence = Math.Max(maxSequence, sequence);
            }
        }

        var nextSequence = maxSequence + 1;
        var nextBarcode = $"STK{nextSequence:D6}";
        while (existingBarcodes.Contains(nextBarcode))
        {
            nextBarcode = $"STK{++nextSequence:D6}";
        }
        return nextBarcode;
    }

    public decimal AddStock(string barcode, string productName, decimal quantity, decimal? newCostPrice = null, decimal? vatRate = null, string note = "")
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Stok girişi miktarı sıfırdan büyük olmalıdır.");
        }
        if (newCostPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newCostPrice), "Alış maliyeti sıfır veya daha büyük olmalıdır.");
        }
        if (vatRate.HasValue && vatRate.Value != 1m && vatRate.Value != 10m && vatRate.Value != 20m)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate), "KDV oranı %1, %10 veya %20 olmalıdır.");
        }
        if (newCostPrice is null && vatRate.HasValue)
        {
            throw new ArgumentException("KDV oranı yalnızca alış maliyeti güncellenirken verilebilir.", nameof(vatRate));
        }
        if (string.IsNullOrWhiteSpace(barcode))
        {
            throw new ArgumentException("Stok girişi için ürünün barkodu olmalıdır.", nameof(barcode));
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        long productId;
        decimal previousStock;
        decimal? existingVatRate;
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT Id, Stock, VatRate FROM Products WHERE Barcode = $barcode AND Name = $name AND IsActive = 1";
            find.Parameters.AddWithValue("$barcode", barcode);
            find.Parameters.AddWithValue("$name", productName);
            using var reader = find.ExecuteReader();
            if (!reader.Read())
            {
                transaction.Rollback();
                throw new InvalidOperationException("Barkoda ait etkin ürün bulunamadı.");
            }
            productId = reader.GetInt64(0);
            previousStock = ReadDecimal(reader, 1);
            existingVatRate = reader.IsDBNull(2) ? null : ReadDecimal(reader, 2);
        }

        var effectiveVatRate = vatRate ?? existingVatRate;
        if (newCostPrice.HasValue && effectiveVatRate is null)
        {
            transaction.Rollback();
            throw new InvalidOperationException("Bu ürün için KDV oranı kayıtlı değil. Alış maliyetini güncellemek için önce KDV oranını seçin.");
        }

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE Products
                SET Stock = Stock + $quantity,
                    CostPrice = CASE WHEN $cost IS NULL THEN CostPrice ELSE $cost END,
                    VatRate = CASE WHEN $vatRate IS NULL THEN VatRate ELSE $vatRate END
                WHERE Id = $id AND IsActive = 1
                """;
            update.Parameters.AddWithValue("$quantity", quantity);
            update.Parameters.AddWithValue("$cost", (object?)newCostPrice ?? DBNull.Value);
            update.Parameters.AddWithValue("$vatRate", (object?)(newCostPrice.HasValue ? effectiveVatRate : null) ?? DBNull.Value);
            update.Parameters.AddWithValue("$id", productId);
            if (update.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                throw new InvalidOperationException("Barkoda ait etkin ürün bulunamadı.");
            }
        }

        decimal updatedStock;
        using (var query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = "SELECT Stock FROM Products WHERE Id = $id AND IsActive = 1";
            query.Parameters.AddWithValue("$id", productId);
            updatedStock = Convert.ToDecimal(query.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        RecordStockMovement(connection, transaction, productId, productName, "Stok girişi", quantity, previousStock, updatedStock, note);
        transaction.Commit();
        return updatedStock;
    }

    public void DeleteProduct(Product product)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Products SET IsActive = 0 WHERE Barcode = $barcode AND Name = $name";
        command.Parameters.AddWithValue("$barcode", product.Barcode);
        command.Parameters.AddWithValue("$name", product.Name);
        command.ExecuteNonQuery();
    }

    public void RemoveTemporaryProductsOnce()
    {
        if (GetSetting("temporary_products_removed") == "1") return;

        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Products WHERE Barcode IN ('1001','1002','1003','1004','1005','1006','1007','1008')";
            command.ExecuteNonQuery();
        }
        SetSetting("temporary_products_removed", "1");
    }

    public void SaveExpense(Expense expense)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Expenses (ExpenseDate, Description, Amount) VALUES ($date, $description, $amount)";
        command.Parameters.AddWithValue("$date", expense.Date.ToString("O"));
        command.Parameters.AddWithValue("$description", expense.Description);
        command.Parameters.AddWithValue("$amount", expense.Amount);
        command.ExecuteNonQuery();
    }

    public List<Customer> LoadCustomers()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Phone, Balance FROM Customers ORDER BY Name";
        using var reader = command.ExecuteReader();
        var result = new List<Customer>();
        while (reader.Read())
        {
            result.Add(new Customer { Id = reader.GetInt64(0), Name = reader.GetString(1), Phone = reader.GetString(2), Balance = ReadDecimal(reader, 3) });
        }
        return result;
    }

    public long SaveCustomer(Customer customer)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Customers (Name, Phone, Balance) VALUES ($name, $phone, $balance); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", customer.Name);
        command.Parameters.AddWithValue("$phone", customer.Phone);
        command.Parameters.AddWithValue("$balance", customer.Balance);
        return (long)command.ExecuteScalar()!;
    }

    public decimal AddCustomerDebt(Customer customer, decimal amount)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Customers SET Balance = Balance + $amount WHERE Id = $id";
        command.Parameters.AddWithValue("$id", customer.Id);
        command.Parameters.AddWithValue("$amount", amount);
        var affected = command.ExecuteNonQuery();
        if (affected == 0)
        {
            throw new InvalidOperationException("Müşteri borcu güncellenemedi; müşteri kaydı bulunamadı.");
        }

        using var balance = connection.CreateCommand();
        balance.CommandText = "SELECT Balance FROM Customers WHERE Id = $id";
        balance.Parameters.AddWithValue("$id", customer.Id);
        using var reader = balance.ExecuteReader();
        return reader.Read() ? ReadDecimal(reader, 0) : 0;
    }

    public void SaveCustomerPayment(Customer customer, decimal amount, string note)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var payment = connection.CreateCommand();
        payment.Transaction = transaction;
        payment.CommandText = "INSERT INTO Payments (CustomerId, PaymentDate, Amount, Note) VALUES ($id, $date, $amount, $note)";
        payment.Parameters.AddWithValue("$id", customer.Id);
        payment.Parameters.AddWithValue("$date", DateTime.Now.ToString("O"));
        payment.Parameters.AddWithValue("$amount", amount);
        payment.Parameters.AddWithValue("$note", note);
        payment.ExecuteNonQuery();
        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE Customers SET Balance = MAX(0, Balance - $amount) WHERE Id = $id";
        update.Parameters.AddWithValue("$id", customer.Id);
        update.Parameters.AddWithValue("$amount", amount);
        update.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<SaleRecord> LoadSalesForDate(DateTime date)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, SaleDate, Revenue, Cost, PaymentMethod, CashAmount, CardAmount FROM Sales WHERE substr(SaleDate, 1, 10) = $date AND IsRefunded = 0 ORDER BY Id";
        command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        using var reader = command.ExecuteReader();
        var result = new List<SaleRecord>();
        while (reader.Read())
        {
            result.Add(new SaleRecord { Id = reader.GetInt64(0), Date = DateTime.Parse(reader.GetString(1)), Revenue = ReadDecimal(reader, 2), Cost = ReadDecimal(reader, 3), PaymentMethod = reader.GetString(4), CashAmount = ReadDecimal(reader, 5), CardAmount = ReadDecimal(reader, 6) });
        }
        return result;
    }

    public List<SaleRecord> LoadAllSales()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, SaleDate, Revenue, Cost, PaymentMethod, IsRefunded, CashAmount, CardAmount FROM Sales ORDER BY Id DESC";
        using var reader = command.ExecuteReader();
        var result = new List<SaleRecord>();
        var salesById = new Dictionary<long, SaleRecord>();
        while (reader.Read())
        {
            var sale = new SaleRecord { Id = reader.GetInt64(0), Date = DateTime.Parse(reader.GetString(1)), Revenue = ReadDecimal(reader, 2), Cost = ReadDecimal(reader, 3), PaymentMethod = reader.GetString(4), IsRefunded = reader.GetInt64(5) == 1, CashAmount = ReadDecimal(reader, 6), CardAmount = ReadDecimal(reader, 7) };
            result.Add(sale);
            salesById.Add(sale.Id, sale);
        }

        reader.Close();
        using var itemsCommand = connection.CreateCommand();
        itemsCommand.CommandText = "SELECT Id, SaleId, ProductName, Quantity, ReturnedQuantity, UnitPrice, UnitCost FROM SaleItems ORDER BY Id";
        using var itemsReader = itemsCommand.ExecuteReader();
        var itemsBySaleId = new Dictionary<long, List<SaleItemRecord>>();
        while (itemsReader.Read())
        {
            var saleId = itemsReader.GetInt64(1);
            if (!salesById.ContainsKey(saleId))
            {
                continue;
            }

            if (!itemsBySaleId.TryGetValue(saleId, out var items))
            {
                items = [];
                itemsBySaleId.Add(saleId, items);
            }

            items.Add(new SaleItemRecord
            {
                Id = itemsReader.GetInt64(0),
                SaleId = saleId,
                SaleDate = salesById[saleId].Date,
                PaymentMethod = salesById[saleId].PaymentMethod,
                CashAmount = salesById[saleId].CashAmount,
                CardAmount = salesById[saleId].CardAmount,
                ProductName = itemsReader.GetString(2),
                Quantity = ReadDecimal(itemsReader, 3),
                ReturnedQuantity = ReadDecimal(itemsReader, 4),
                UnitPrice = ReadDecimal(itemsReader, 5),
                UnitCost = ReadDecimal(itemsReader, 6)
            });
        }

        foreach (var sale in result)
        {
            if (itemsBySaleId.TryGetValue(sale.Id, out var items))
            {
                sale.Items.AddRange(items);
            }
        }
        return result;
    }

    public bool RefundSaleItem(long saleItemId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        long saleId;
        long productId;
        string productName;
        decimal quantity;
        decimal returnedQuantity;
        using (var item = connection.CreateCommand())
        {
            item.Transaction = transaction;
            item.CommandText = "SELECT SaleId, ProductId, ProductName, Quantity, ReturnedQuantity FROM SaleItems WHERE Id = $id";
            item.Parameters.AddWithValue("$id", saleItemId);
            using var reader = item.ExecuteReader();
            if (!reader.Read())
            {
                transaction.Rollback();
                return false;
            }
            saleId = reader.GetInt64(0);
            productId = reader.GetInt64(1);
            productName = reader.GetString(2);
            quantity = ReadDecimal(reader, 3);
            returnedQuantity = ReadDecimal(reader, 4);
        }

        var quantityToReturn = quantity - returnedQuantity;
        if (quantityToReturn <= 0)
        {
            transaction.Rollback();
            return false;
        }

        using (var updateItem = connection.CreateCommand())
        {
            updateItem.Transaction = transaction;
            updateItem.CommandText = "UPDATE SaleItems SET ReturnedQuantity = Quantity WHERE Id = $id AND ReturnedQuantity < Quantity";
            updateItem.Parameters.AddWithValue("$id", saleItemId);
            if (updateItem.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return false;
            }
        }
        UpdateProductStock(connection, transaction, productId, productName, quantityToReturn);
        MarkSaleRefundedIfComplete(connection, transaction, saleId);
        transaction.Commit();
        return true;
    }

    public bool RefundSale(long saleId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var stockUpdates = new List<(long SaleItemId, long ProductId, string ProductName, decimal Quantity)>();
        using (var items = connection.CreateCommand())
        {
            items.Transaction = transaction;
            items.CommandText = "SELECT Id, ProductId, ProductName, Quantity, ReturnedQuantity FROM SaleItems WHERE SaleId = $saleId";
            items.Parameters.AddWithValue("$saleId", saleId);
            using var reader = items.ExecuteReader();
            while (reader.Read())
            {
                var quantityToReturn = ReadDecimal(reader, 3) - ReadDecimal(reader, 4);
                if (quantityToReturn > 0)
                {
                    stockUpdates.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), quantityToReturn));
                }
            }
        }
        if (stockUpdates.Count == 0)
        {
            transaction.Rollback();
            return false;
        }

        foreach (var update in stockUpdates)
        {
            using var updateItem = connection.CreateCommand();
            updateItem.Transaction = transaction;
            updateItem.CommandText = "UPDATE SaleItems SET ReturnedQuantity = Quantity WHERE Id = $id AND ReturnedQuantity < Quantity";
            updateItem.Parameters.AddWithValue("$id", update.SaleItemId);
            if (updateItem.ExecuteNonQuery() == 1)
            {
                UpdateProductStock(connection, transaction, update.ProductId, update.ProductName, update.Quantity);
            }
        }
        MarkSaleRefundedIfComplete(connection, transaction, saleId);
        transaction.Commit();
        return true;
    }

    private static void UpdateProductStock(SqliteConnection connection, SqliteTransaction transaction, long productId, string productName, decimal quantity)
    {
        ApplyStockChange(connection, transaction, productId, productName, quantity, "İade", "Satış iadesi");
    }

    private static void MarkSaleRefundedIfComplete(SqliteConnection connection, SqliteTransaction transaction, long saleId)
    {
        using var sale = connection.CreateCommand();
        sale.Transaction = transaction;
        sale.CommandText = """
            UPDATE Sales SET IsRefunded = 1
            WHERE Id = $id
                AND NOT EXISTS (
                    SELECT 1 FROM SaleItems
                    WHERE SaleItems.SaleId = Sales.Id
                        AND SaleItems.ReturnedQuantity < SaleItems.Quantity
                )
            """;
        sale.Parameters.AddWithValue("$id", saleId);
        sale.ExecuteNonQuery();
    }

    public List<Expense> LoadExpensesForDate(DateTime date)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ExpenseDate, Description, Amount FROM Expenses WHERE substr(ExpenseDate, 1, 10) = $date ORDER BY Id";
        command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        using var reader = command.ExecuteReader();
        var result = new List<Expense>();
        while (reader.Read())
        {
            result.Add(new Expense { Id = reader.GetInt64(0), Date = DateTime.Parse(reader.GetString(1)), Description = reader.GetString(2), Amount = ReadDecimal(reader, 3) });
        }
        return result;
    }

    public long SaveSale(IEnumerable<CartItem> items, decimal revenue, decimal cost, string paymentMethod, long? customerId = null, decimal cashAmount = 0, decimal cardAmount = 0)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var saleCommand = connection.CreateCommand();
        saleCommand.Transaction = transaction;
        saleCommand.CommandText = "INSERT INTO Sales (SaleDate, Revenue, Cost, PaymentMethod, CustomerId, CashAmount, CardAmount) VALUES ($date, $revenue, $cost, $method, $customerId, $cash, $card); SELECT last_insert_rowid();";
        saleCommand.Parameters.AddWithValue("$date", DateTime.Now.ToString("O"));
        saleCommand.Parameters.AddWithValue("$revenue", revenue);
        saleCommand.Parameters.AddWithValue("$cost", cost);
        saleCommand.Parameters.AddWithValue("$method", paymentMethod);
        saleCommand.Parameters.AddWithValue("$customerId", (object?)customerId ?? DBNull.Value);
        saleCommand.Parameters.AddWithValue("$cash", cashAmount);
        saleCommand.Parameters.AddWithValue("$card", cardAmount);
        var saleId = (long)saleCommand.ExecuteScalar()!;

        foreach (var item in items)
        {
            using var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = transaction;
            itemCommand.CommandText = "INSERT INTO SaleItems (SaleId, ProductId, ProductName, Quantity, UnitPrice, UnitCost) SELECT $saleId, Id, $name, $quantity, $price, $cost FROM Products WHERE Barcode = $barcode AND Name = $name AND IsActive = 1";
            itemCommand.Parameters.AddWithValue("$saleId", saleId);
            itemCommand.Parameters.AddWithValue("$barcode", item.Product.Barcode);
            itemCommand.Parameters.AddWithValue("$name", item.Name);
            itemCommand.Parameters.AddWithValue("$quantity", item.Quantity);
            itemCommand.Parameters.AddWithValue("$price", item.UnitPrice);
            itemCommand.Parameters.AddWithValue("$cost", item.UnitCost);
            if (itemCommand.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException($"'{item.Name}' ürünü artık etkin değil; satış kaydedilemedi.");
            }

            ApplyStockChange(connection, transaction, item.Product.Id, item.Name, -item.Quantity, "Satış", $"Satış no: {saleId}");
        }

        transaction.Commit();
        return saleId;
    }

    public string? GetSetting(string key)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar()?.ToString();
    }

    public void SetSetting(string key, string value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Settings (Key, Value) VALUES ($key, $value) ON CONFLICT(Key) DO UPDATE SET Value = $value";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public bool ValidateUser(string username, string password)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT PasswordHash FROM Users WHERE Username = $username AND IsActive = 1";
        command.Parameters.AddWithValue("$username", username.Trim());
        var storedHash = command.ExecuteScalar()?.ToString();
        return storedHash is not null && storedHash == HashPassword(password);
    }

    public AppUser? AuthenticateUser(string username, string password)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, Role FROM Users WHERE Username = $username AND PasswordHash = $hash AND IsActive = 1";
        command.Parameters.AddWithValue("$username", username.Trim());
        command.Parameters.AddWithValue("$hash", HashPassword(password));
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new AppUser { Id = reader.GetInt64(0), Username = reader.GetString(1), Role = reader.GetString(2) }
            : null;
    }

    public AppUser? LoadUser(long userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, Role, IsActive FROM Users WHERE Id = $id";
        command.Parameters.AddWithValue("$id", userId);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new AppUser
            {
                Id = reader.GetInt64(0),
                Username = reader.GetString(1),
                Role = reader.GetString(2),
                IsActive = reader.GetInt64(3) == 1
            }
            : null;
    }

    public List<AppUser> LoadUsers()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, Role, IsActive FROM Users ORDER BY Username";
        using var reader = command.ExecuteReader();
        var result = new List<AppUser>();
        while (reader.Read())
        {
            result.Add(new AppUser
            {
                Id = reader.GetInt64(0),
                Username = reader.GetString(1),
                Role = reader.GetString(2),
                IsActive = reader.GetInt64(3) == 1
            });
        }
        return result;
    }

    public void SaveUser(string username, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username) || (role != "Admin" && role != "Cashier"))
        {
            throw new ArgumentException("Geçerli kullanıcı adı ve rol girilmelidir.");
        }
        if (password.Length < 8)
        {
            throw new ArgumentException("Parola en az 8 karakter olmalıdır.", nameof(password));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Users (Username, PasswordHash, Role, IsActive) VALUES ($username, $hash, $role, 1)";
        command.Parameters.AddWithValue("$username", username.Trim());
        command.Parameters.AddWithValue("$hash", HashPassword(password));
        command.Parameters.AddWithValue("$role", role);
        command.ExecuteNonQuery();
    }

    public void SetUserActive(long userId, bool isActive, string currentUsername)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        string username;
        string role;
        bool wasActive;
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT Username, Role, IsActive FROM Users WHERE Id = $id";
            find.Parameters.AddWithValue("$id", userId);
            using var reader = find.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Kullanıcı bulunamadı.");
            }
            username = reader.GetString(0);
            role = reader.GetString(1);
            wasActive = reader.GetInt64(2) == 1;
        }

        if (username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase) && isActive == false)
        {
            throw new InvalidOperationException("Kendi hesabınızı devre dışı bırakamazsınız.");
        }
        if (wasActive && !isActive && role == "Admin")
        {
            using var count = connection.CreateCommand();
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM Users WHERE Role = 'Admin' AND IsActive = 1";
            if (Convert.ToInt64(count.ExecuteScalar(), CultureInfo.InvariantCulture) <= 1)
            {
                throw new InvalidOperationException("Son etkin yönetici hesabı devre dışı bırakılamaz.");
            }
        }

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE Users SET IsActive = $active WHERE Id = $id";
        update.Parameters.AddWithValue("$active", isActive ? 1 : 0);
        update.Parameters.AddWithValue("$id", userId);
        update.ExecuteNonQuery();
        transaction.Commit();
    }

    public void UpdateUser(long userId, string role, string? newPassword, string currentUsername)
    {
        if (role != "Admin" && role != "Cashier")
        {
            throw new ArgumentException("Geçerli bir kullanıcı rolü seçilmelidir.", nameof(role));
        }
        if (!string.IsNullOrEmpty(newPassword) && newPassword.Length < 8)
        {
            throw new ArgumentException("Yeni parola en az 8 karakter olmalıdır.", nameof(newPassword));
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        string username;
        string existingRole;
        bool isActive;
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT Username, Role, IsActive FROM Users WHERE Id = $id";
            find.Parameters.AddWithValue("$id", userId);
            using var reader = find.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Kullanıcı bulunamadı.");
            }
            username = reader.GetString(0);
            existingRole = reader.GetString(1);
            isActive = reader.GetInt64(2) == 1;
        }

        if (username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase) && role != "Admin")
        {
            throw new InvalidOperationException("Kendi hesabınızın yönetici yetkisini kaldıramazsınız.");
        }
        if (isActive && existingRole == "Admin" && role != "Admin")
        {
            using var count = connection.CreateCommand();
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM Users WHERE Role = 'Admin' AND IsActive = 1";
            if (Convert.ToInt64(count.ExecuteScalar(), CultureInfo.InvariantCulture) <= 1)
            {
                throw new InvalidOperationException("Son etkin yönetici hesabının rolü değiştirilemez.");
            }
        }

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = string.IsNullOrEmpty(newPassword)
            ? "UPDATE Users SET Role = $role WHERE Id = $id"
            : "UPDATE Users SET Role = $role, PasswordHash = $hash WHERE Id = $id";
        update.Parameters.AddWithValue("$role", role);
        update.Parameters.AddWithValue("$id", userId);
        if (!string.IsNullOrEmpty(newPassword))
        {
            update.Parameters.AddWithValue("$hash", HashPassword(newPassword));
        }
        update.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<StockMovement> LoadStockMovements(DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ProductName, MovementDate, MovementType, Quantity, PreviousStock, NewStock, Note, Username
            FROM StockMovements
            WHERE ($from IS NULL OR substr(MovementDate, 1, 10) >= $from)
              AND ($to IS NULL OR substr(MovementDate, 1, 10) <= $to)
            ORDER BY Id DESC
            """;
        command.Parameters.AddWithValue("$from", (object?)fromDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$to", (object?)toDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        using var reader = command.ExecuteReader();
        var result = new List<StockMovement>();
        while (reader.Read())
        {
            result.Add(new StockMovement
            {
                ProductName = reader.GetString(0),
                Date = DateTime.Parse(reader.GetString(1)),
                Type = reader.GetString(2),
                Quantity = ReadDecimal(reader, 3),
                PreviousStock = ReadDecimal(reader, 4),
                NewStock = ReadDecimal(reader, 5),
                Note = reader.GetString(6),
                Username = reader.GetString(7)
            });
        }
        return result;
    }

    public SalesReportData LoadSalesReport(DateTime? fromDate, DateTime? toDate)
    {
        using var connection = OpenConnection();
        using var totals = connection.CreateCommand();
        totals.CommandText = """
            SELECT COALESCE(SUM((Quantity - ReturnedQuantity) * UnitPrice), 0),
                   COALESCE(SUM((Quantity - ReturnedQuantity) * UnitCost), 0),
                   COALESCE(SUM(Quantity - ReturnedQuantity), 0),
                   COALESCE(SUM(ReturnedQuantity * UnitPrice), 0),
                   COALESCE(SUM(ReturnedQuantity), 0)
            FROM SaleItems
            JOIN Sales ON Sales.Id = SaleItems.SaleId
            WHERE ($from IS NULL OR substr(Sales.SaleDate, 1, 10) >= $from)
              AND ($to IS NULL OR substr(Sales.SaleDate, 1, 10) <= $to)
            """;
        AddReportDateParameters(totals, fromDate, toDate);
        using var totalReader = totals.ExecuteReader();
        totalReader.Read();
        var report = new SalesReportData
        {
            Revenue = ReadDecimal(totalReader, 0),
            Cost = ReadDecimal(totalReader, 1),
            Quantity = ReadDecimal(totalReader, 2),
            RefundedRevenue = ReadDecimal(totalReader, 3),
            ReturnedQuantity = ReadDecimal(totalReader, 4)
        };

        using var products = connection.CreateCommand();
        products.CommandText = """
            SELECT ProductName, SUM(Quantity - ReturnedQuantity) AS SoldQuantity,
                   SUM(ReturnedQuantity) AS ReturnedQuantity,
                   SUM((Quantity - ReturnedQuantity) * UnitPrice) AS Revenue,
                   SUM(ReturnedQuantity * UnitPrice) AS RefundedRevenue,
                   SUM((Quantity - ReturnedQuantity) * UnitCost) AS Cost
            FROM SaleItems
            JOIN Sales ON Sales.Id = SaleItems.SaleId
            WHERE ($from IS NULL OR substr(Sales.SaleDate, 1, 10) >= $from)
              AND ($to IS NULL OR substr(Sales.SaleDate, 1, 10) <= $to)
            GROUP BY ProductId, ProductName
            HAVING SUM(Quantity - ReturnedQuantity) > 0
            ORDER BY SoldQuantity DESC, Revenue DESC
            """;
        AddReportDateParameters(products, fromDate, toDate);
        using var productReader = products.ExecuteReader();
        while (productReader.Read())
        {
            report.BestSellers.Add(new BestSellingProduct
            {
                ProductName = productReader.GetString(0),
                Quantity = ReadDecimal(productReader, 1),
                ReturnedQuantity = ReadDecimal(productReader, 2),
                Revenue = ReadDecimal(productReader, 3),
                RefundedRevenue = ReadDecimal(productReader, 4),
                Cost = ReadDecimal(productReader, 5)
            });
        }
        return report;
    }

    public bool ValidateAdmin(string password)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT PasswordHash FROM Users WHERE Username = 'admin' AND Role = 'Admin' AND IsActive = 1";
        var storedHash = command.ExecuteScalar()?.ToString();
        return storedHash is not null && storedHash == HashPassword(password);
    }

    public bool ChangeAdminPassword(string currentPassword, string newPassword)
    {
        if (newPassword.Length < 8)
        {
            throw new ArgumentException("Yeni parola en az 8 karakter olmalıdır.", nameof(newPassword));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users
            SET PasswordHash = $newHash
            WHERE Username = 'admin' AND Role = 'Admin' AND IsActive = 1
              AND PasswordHash = $currentHash
            """;
        command.Parameters.AddWithValue("$newHash", HashPassword(newPassword));
        command.Parameters.AddWithValue("$currentHash", HashPassword(currentPassword));
        return command.ExecuteNonQuery() == 1;
    }

    public void ResetAllBusinessData()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var table in new[] { "StockMovements", "SaleItems", "Sales", "Payments", "Customers", "Expenses", "Products", "Categories", "Settings" })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table}";
            command.ExecuteNonQuery();
        }
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Users WHERE Username <> 'admin'";
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private void EnsureDefaultUser()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Users (Username, PasswordHash, Role) VALUES ('admin', $hash, 'Admin'); UPDATE Users SET PasswordHash = $hash WHERE Username = 'admin' AND PasswordHash = $oldHash";
        command.Parameters.AddWithValue("$hash", HashPassword(DefaultAdminPassword));
        command.Parameters.AddWithValue("$oldHash", HashPassword("admin"));
        command.ExecuteNonQuery();
    }

    private static string HashPassword(string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public void Backup(string destinationFile)
    {
        using var source = OpenConnection();
        using var destination = new SqliteConnection($"Data Source={destinationFile}");
        destination.Open();
        source.BackupDatabase(destination);
    }

    public void Restore(string backupFile)
    {
        using var source = new SqliteConnection($"Data Source={backupFile}");
        source.Open();
        using var destination = OpenConnection();
        source.BackupDatabase(destination);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static void AddProductParameters(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("$barcode", product.Barcode);
        command.Parameters.AddWithValue("$name", product.Name);
        command.Parameters.AddWithValue("$category", product.Category);
        command.Parameters.AddWithValue("$cost", product.CostPrice);
        command.Parameters.AddWithValue("$vatRate", (object?)product.VatRate ?? DBNull.Value);
        command.Parameters.AddWithValue("$price", product.Price);
        command.Parameters.AddWithValue("$stock", product.Stock);
        command.Parameters.AddWithValue("$minimumStock", product.MinimumStock);
    }

    private static void EnsureCategoryExists(SqliteConnection connection, SqliteTransaction transaction, string categoryName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR IGNORE INTO Categories (Name) VALUES ($name)";
        command.Parameters.AddWithValue("$name", categoryName);
        command.ExecuteNonQuery();
    }

    private static object GetLastInsertId(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT last_insert_rowid()";
        return command.ExecuteScalar()!;
    }

    private static void AddReportDateParameters(SqliteCommand command, DateTime? fromDate, DateTime? toDate)
    {
        command.Parameters.AddWithValue("$from", (object?)fromDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$to", (object?)toDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
    }

    private static void ApplyStockChange(SqliteConnection connection, SqliteTransaction transaction, long productId, string productName, decimal quantity, string type, string note)
    {
        decimal previousStock;
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT Stock FROM Products WHERE Id = $id";
            find.Parameters.AddWithValue("$id", productId);
            var value = find.ExecuteScalar();
            if (value is null)
            {
                throw new InvalidOperationException($"'{productName}' ürününün stok kaydı bulunamadı.");
            }
            previousStock = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        var newStock = previousStock + quantity;
        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE Products SET Stock = $stock WHERE Id = $id";
            update.Parameters.AddWithValue("$stock", newStock);
            update.Parameters.AddWithValue("$id", productId);
            if (update.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException($"'{productName}' ürününün stoğu güncellenemedi.");
            }
        }
        RecordStockMovement(connection, transaction, productId, productName, type, quantity, previousStock, newStock, note);
    }

    private static void RecordStockMovement(SqliteConnection connection, SqliteTransaction transaction, long productId, string productName, string type, decimal quantity, decimal previousStock, decimal newStock, string note)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO StockMovements (ProductId, ProductName, MovementDate, MovementType, Quantity, PreviousStock, NewStock, Note, Username)
            VALUES ($productId, $name, $date, $type, $quantity, $previous, $current, $note, $username)
            """;
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$name", productName);
        command.Parameters.AddWithValue("$date", DateTime.Now.ToString("O"));
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$quantity", quantity);
        command.Parameters.AddWithValue("$previous", previousStock);
        command.Parameters.AddWithValue("$current", newStock);
        command.Parameters.AddWithValue("$note", note);
        command.Parameters.AddWithValue("$username", UserSession.CurrentUser?.Username ?? "system");
        command.ExecuteNonQuery();
    }

    private static decimal ReadDecimal(SqliteDataReader reader, int ordinal)
        => Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
