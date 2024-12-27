# HTTPOfficial

## Overview

HTTPOfficial is a robust system designed to manage user authentication and interaction with third-party canvas servers. It provides a seamless way for users to link multiple profiles from various canvas servers to a single global account, also enabling global systems such as posts and rplace canvas server instance management.

## Features

- **Multi-Profile Linking**: Users can link multiple canvas profiles to a single account.
- **Flexible Authentication**: Supports authentication via username and email, as well as third-party canvas servers.
- **Role-Based Actions**: Users can choose to act as their global account or any linked canvas user when performing actions.

## Authentication

The system supports two types of users: [Accounts](./DataModel/Account.cs) (authenticated with a username and email) and [Canvas Users](./DataModel/CanvasUser.cs) (representing users from third-party canvas servers). An account can be linked to multiple canvas users, as a user may have profiles on various canvas servers and may want to connect them to a central 'global' account. When making a request, such as creating a new post, users can choose to act as their account or as one of their canvas users. Authentication as a canvas user does not grant access to the account, and vice versa.

## Getting Started

To get started with HTTPOfficial, follow these steps:

1. **Clone the Repository**:
    ```sh
    git clone /home/zekiah/RplaceServer/HTTPOfficial
    ```
2. **Navigate to the Project Directory**:
    ```sh
    cd HTTPOfficial
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
    dotnet run
    ```

## Contributing

We welcome contributions to HTTPOfficial! Please fork the repository and submit pull requests for any enhancements or bug fixes.

## License

This project is licensed under the GPL-3.0 License. See the [LICENSE](../LICENSE) file for details.

## Contact

For any questions or feedback, please open an issue on the GitHub repository or contact the project maintainers.
