# LHSDB Free Agents API

This project is a .NET 8 Web API designed to manage players and offers for the LHSDB fantasy hockey league. It uses Amazon DynamoDB for data storage and AWS Cognito for user authentication.

## Features

- **Player Management:**
  - Get all free agents.
  - Get all players for a specific team.
- **Offer Management:**
  - Create a new offer for a player.
  - Delete an offer.
  - Get all offers made by a team.
  - Get all offers made to a player.

## Technology Stack

- **Framework:** ASP.NET Core 8.0
- **Database:** Amazon DynamoDB
- **Authentication:** AWS Cognito (using JWT)
- **Deployment:** AWS Elastic Beanstalk

## Architecture

The project follows a standard layered architecture pattern, promoting separation of concerns and maintainability:

- **Controllers:** Located in the `Controllers` directory, these are responsible for handling incoming HTTP requests, validating input, and returning appropriate HTTP responses. They delegate the business logic to the service layer.

- **Services:** Located in the `Services` directory, this layer contains the core business logic of the application. It orchestrates operations between the controllers and the repositories.

- **Repositories:** Located in the `Repositories` directory, this layer abstracts the data access logic. It is responsible for all interactions with the Amazon DynamoDB database, isolating the rest of the application from the specifics of the data store.

- **Mappers:** The `Mappers` directory contains logic for converting data between different object models, such as from the database models (`PlayerDb`, `OfferDb`) to the API response models (`PlayerResponse`, `OfferModel`).

- **Models:** The `Models` directory defines the data structures used throughout the application, including database entities and API data transfer objects (DTOs).

## API Endpoints

All endpoints are protected and require authentication.

### Players

- `GET /players`: Returns a simple "Service is running" message.
- `GET /players/freeagents`: Retrieves a list of all players marked as free agents.
- `GET /players/teams/{teamId}`: Retrieves a list of players for a given team ID.
  - **Query Parameter:** `faOnly` (boolean) - If true, returns only free agents from that team.

### Offers

- `POST /offers`: Creates a new offer. The offer details are sent in the request body.
- `DELETE /offers/{offerId}`: Deletes a specific offer by its ID.
- `GET /offers/teams/{teamId}`: Retrieves all offers made by a specific team.
- `GET /offers/players/{playerId}`: Retrieves all offers made to a specific player.

## Local Setup & Configuration

1.  **Prerequisites:**
    - .NET 8 SDK
    - AWS Account
    - AWS CLI configured with credentials

2.  **Configuration:**
    Update the `appsettings.json` file with your AWS Cognito details:
    ```json
    "AWSCognito": {
      "Region": "your-aws-region",
      "PoolId": "your-cognito-pool-id",
      "AppClientId": "your-cognito-app-client-id"
    }
    ```

3.  **Running the Application:**
    You can run the project using Visual Studio or the .NET CLI. The application will be available at `https://localhost:5001` and `http://localhost:5000` as configured in `Properties/launchSettings.json`.

## Deployment

The project is configured for deployment to AWS Elastic Beanstalk using the settings found in `aws-beanstalk-tools-defaults.json`.
