# Transaction API

## Overview
This project is an ASP.NET Core based REST API for handling transaction requests from authorized partners. It includes endpoints for submitting transaction information, generating signatures, and validating requests.

## Features
- **Transaction Submission**: Allows authorized partners to submit transaction details.
- **Signature Generation**: Generates secure signatures for transaction requests to ensure data integrity.
- **Validation Logic**: Validates incoming requests to ensure all required fields are present and correctly formatted.
- **Logging**: Implements logging for tracking requests and errors using log4net.

## API Endpoints
### 1. Submit Transaction
- **URL**: `/api/submittrxmessage`
- **Method**: `POST`
- **Request Body**:
  ```json
  {
      "partnerkey": "FAKEGOOGLE",
      "partnerrefno": "FG-00001",
      "partnerpassword": "RkFLRVBBU1NXT1JEMTIzNA==",
      "totalamount": 1000,
      "items": [
          {
              "partneritemref": "i-00001",
              "name": "Pen",
              "qty": 4,
              "unitprice": 200
          }
      ],
      "timestamp": "2024-08-15T02:11:22.0000000Z",
      "sig": "generated_signature"
  }
  ```
- **Response**:
  ```json
  {
      "result": 1,
      "totalamount": 1000,
      "totaldiscount": 0,
      "finalamount": 1000
  }
  ```

### 2. Generate Signature
- **URL**: `/api/generate-signature`
- **Method**: `POST`
- **Request Body**: Same as the submit transaction request.
- **Response**:
  ```json
  {
      "Signature": "generated_signature"
  }
  ```

## Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/Aimanr17/TransactionApi.git
   ```
2. Navigate to the project directory:
   ```bash
   cd TransactionApi
   ```
3. Restore dependencies:
   ```bash
   dotnet restore
   ```
4. Run the application:
   ```bash
   dotnet run
   ```

## Docker Support
This application can be containerized using Docker. A `Dockerfile` is included for building the application image.

### Build the Docker Image
```bash
docker build -t transaction-api .
```

### Run the Docker Container
```bash
docker run -d -p 8080:8080 transaction-api
```

## Contributing
Contributions are welcome! Please submit a pull request or open an issue for discussion.

## License
This project is licensed under the MIT License.

---

Feel free to modify this README to better suit your project needs.
