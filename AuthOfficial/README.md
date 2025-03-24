# AuthOfficial

## Overview

AuthOfficial is a robust server software designed to manage user authentication and interaction with third-party canvas
servers. It provides a seamless way for users to link multiple profiles from various canvas servers to a single global
account, also enabling global systems such as posts and rplace canvas server instance management.

## Features

- **Multi-Profile Linking**: Users can link multiple canvas profiles to a single account.
- **Flexible Authentication**: Supports authentication via username and email, as well as third-party canvas servers.
- **Role-Based Actions**: Users can choose to act as their global account or any linked canvas user when performing
- actions.

## Authentication

The system supports two types of users: [Accounts](./DataModel/Account.cs) (authenticated with a username and email) and
[Canvas Users](./DataModel/CanvasUser.cs) (representing users from third-party canvas servers). An account can be linked
to multiple canvas users, as a user may have profiles on various canvas servers and may want to connect them to a central
'global' account. When making a request, such as creating a new post, users can choose to act as their account or as one
of their canvas users. Authentication as a canvas user does not grant access to the account, and vice versa.

### JWT spec:
**Account token:**
```json5
{
    // Registered  
    "typ": "Account",
    "sub": "123", // Account ID
    "name": "admin",
    "email": "admin@rplace.live",
    "email_verified": "false",
    // Custom
    "linkedUsers": "[ { \"id\": \"456\", \"instanceId\": \"1\", \"userIntId\": \"98765\" } ]",
    "tier": "Free",
    "securityStamp": "TzE5S0Y5VmQ5ZHVaYTdXbzJiZTVwWVl4QjN3ZGZsd3I=",
}
```

**Single canvas user (linkage) token:**
```json5
{
    // Registered
    "typ": "CanvasUser",
    "sub": "456", // Canvas User ID
    // Custom
    "instanceId": "1",
    "userIntId": "98765", 
    "securityStamp": "dEZ6NDd5WGpkSkJJWGxPTlA5NWJVYUw5UkIxWGpoV0U="
}
```


## Getting Started

To get started with AuthOfficial, follow these steps:

1. **Clone the Repository**:
    ```sh
    git clone https://github.com/Zekiah-A/RplaceServer
    ```
2. **Navigate to the Project Directory**:
    ```sh
    cd AuthOfficial
    ```
3. **Install Dependencies**:
    ```sh
    dotnet restore
    ```
4. **Build the Project**:
    ```sh
    dotnet build
    ```
5. **Run the Application**:
    ```sh
    ASPNETCORE_ENVIRONMENT=Development dotnet run
    ```

### PostgreSQL Setup:
1. Initialise the root postgres database on your system
   ```sh
   sudo -iu postgres initdb --locale en_US.UTF-8 -D /var/lib/postgres/data
   ```
2. Enable the postgres background service
   ```sh
   sudo systemctl enable postgresql
   sudo systemctl start postgresql
   ```
3. Secure the database
   ```sh
   # Switch to the postgres user
   sudo -iu postgres
   # Access the postgres commandline (exit with '\q')
   psql
   ```
   ```sql
   ALTER USER postgres WITH PASSWORD '<a-strong-root-password-goes-here>';
   ```

**AuthOfficial database setup:**
1. Run `psql` again and create the AuthOfficial database
   ```sql
   -- Create the database
   CREATE DATABASE "AuthOfficial";
   -- Create the user
   CREATE USER authofficial WITH ENCRYPTED PASSWORD '<a-strong-database-password-goes-here>';
   -- Grant all privileges on the database
   ALTER DATABASE "AuthOfficial" OWNER TO authofficial;
   GRANT ALL PRIVILEGES ON DATABASE "AuthOfficial" TO authofficial;
   ```
2. Configure PostgreSQL for Authentication
   Edit PostgreSQL’s pg_hba.conf:
   ```sh
   sudo nano /var/lib/postgres/data/pg_hba.conf
   ```
   Add the following lines:
   ```sh
   # Allow local connections for the authofficial user
   local   all             authofficial           md5
   host    all             authofficial 127.0.0.1/32  md5
   host    all             authofficial ::1/128       md5
   ```
3. Restart PostgreSQL
   ```sh
   sudo systemctl restart postgresql
   ```

## Contributing

We welcome contributions to AuthOfficial! Please fork the repository and submit pull requests for any enhancements or bug fixes.

## License

This project is licensed under the GPL-3.0 License. See the [LICENSE](../LICENSE) file for details.

## Contact

For any questions or feedback, please open an issue on the GitHub repository or contact the project maintainers.
