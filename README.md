# Government Petition Service

University project for people to send petitions to the government with ability to filter it according to themes or topics.

This is a full-stack wireframe application built with:
- **Backend**: C# .NET 9.0 Web API with ASP.NET Core
- **Frontend**: React 18 with modern JavaScript
- **Architecture**: RESTful API with responsive React UI

![Government Petition Service Wireframe](https://github.com/user-attachments/assets/531e5ece-cf25-4089-a13f-b3e560942fc5)

## Features

### 🎯 Core Functionality
- ✅ View all government petitions in a card-based layout
- ✅ Create new petitions with detailed forms
- ✅ Sign existing petitions (signature counter updates)
- ✅ Filter petitions by category, theme, status, and government level
- ✅ Search petitions by title and description
- ✅ View petition details and metadata

### 🔧 Technical Features
- ✅ RESTful API with full CRUD operations
- ✅ Responsive design for mobile and desktop
- ✅ Real-time data updates
- ✅ Form validation and error handling
- ✅ Professional UI with modern styling
- ✅ CORS configuration for cross-origin requests

## Project Structure

```
government-petition-service/
├── PetitionService.API/          # C# .NET Web API Backend
│   ├── Controllers/              # API Controllers
│   ├── Models/                   # Data Models
│   ├── Services/                 # Business Logic
│   └── Program.cs               # API Configuration
├── petition-frontend/            # React Frontend
│   ├── src/
│   │   ├── components/          # React Components
│   │   ├── services/           # API Service Layer
│   │   └── App.js             # Main Application
└── GovernmentPetitionService.sln # Solution File
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Node.js 20+ and npm
- Modern web browser

### Running the Application

1. **Start the API Server**:
   ```bash
   cd PetitionService.API
   dotnet run
   ```
   API will run on `http://localhost:5027`

2. **Start the React Frontend**:
   ```bash
   cd petition-frontend
   npm install
   npm start
   ```
   Frontend will run on `http://localhost:3000`

3. **Access the Application**:
   Open `http://localhost:3000` in your browser

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/petitions` | Get all petitions with optional filtering |
| GET | `/api/petitions/{id}` | Get petition by ID |
| POST | `/api/petitions` | Create new petition |
| POST | `/api/petitions/{id}/sign` | Sign a petition |
| GET | `/api/petitions/categories` | Get available categories |
| GET | `/api/petitions/themes` | Get available themes |

## Sample Data

The application includes 5 sample petitions covering various government levels and topics:
- Healthcare access improvements
- Public transportation infrastructure
- Education funding
- Environmental initiatives
- Green spaces development

## Filtering & Search Capabilities

Users can filter petitions by:
- **Category**: Education, Environment, Healthcare, Transportation
- **Theme**: Access, Energy, Funding, Infrastructure, Recreation
- **Status**: Active, Under Review, Closed  
- **Government Level**: Federal, State, Local
- **Search**: Full-text search across titles and descriptions

## User Interface

The wireframe features:
- Clean, professional design with government-appropriate styling
- Responsive grid layout for petition cards
- Expandable filter panel
- Modal dialog for creating new petitions
- Color-coded status indicators
- Mobile-friendly responsive design

## Development Notes

This is a wireframe/prototype application designed to demonstrate the user interface and basic functionality of a government petition system. It uses in-memory data storage and is not intended for production use without proper database integration and security measures.
