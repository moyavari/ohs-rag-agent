# OHS Copilot Web Frontend

A modern, responsive web interface for interacting with the OHS Copilot AI Safety Assistant.

## 🚀 Quick Start

```bash
# Start both API and frontend
./start-frontend.sh

# Frontend opens at: http://localhost:8080
# API runs at: http://localhost:5000
```

## 🎯 Features

### 💬 **Ask Questions Tab**
- **Natural Language Q&A**: Ask safety-related questions in plain English
- **Multi-turn Conversations**: Use conversation IDs for context-aware dialogues
- **Source Citations**: View relevant document sources with relevance scores
- **Token Configuration**: Adjust response length (1K-4K tokens)
- **Real-time Processing**: Live processing time and correlation ID tracking

### ✍️ **Draft Letters Tab**
- **Purpose-driven Letter Generation**: Specify the letter's intent
- **Dynamic Key Points**: Add/remove bullet points for letter content
- **Tone Selection**: Choose between formal, casual, or urgent tone
- **Recipient Configuration**: Specify letter recipient
- **Professional Formatting**: Clean, business-appropriate letter layout

### 📊 **Metrics Tab**
- **Real-time System Metrics**: Request counts, token usage, response times
- **Performance Monitoring**: Average latency and error rate tracking
- **Recent Activity**: View latest API operations and results
- **Live Updates**: Refresh metrics on demand

### ⚙️ **Settings Tab**
- **API Configuration**: Customize API endpoint and demo mode settings
- **Connection Testing**: Verify API connectivity and health
- **Quick Actions**: Access demo fixtures, golden dataset, audit logs, evaluations
- **Persistent Settings**: Configuration saved locally in browser

## 🎨 Design Features

### **Modern UI/UX**
- **Responsive Design**: Works on desktop, tablet, and mobile
- **Clean Interface**: Professional appearance suitable for enterprise use
- **Intuitive Navigation**: Tab-based interface with clear visual hierarchy
- **Real-time Feedback**: Loading states, success/error messages
- **Status Monitoring**: Live API and system health indicators

### **Accessibility**
- **Keyboard Navigation**: Full keyboard accessibility
- **Screen Reader Support**: Semantic HTML structure
- **High Contrast**: Clear color contrast for readability
- **Mobile Optimized**: Touch-friendly interface on mobile devices

## 🔧 Configuration

### **API Settings**
- **Base URL**: Default `http://localhost:5000` (configurable)
- **Demo Mode**: Auto-detect or force enable/disable
- **Timeout**: 30-second request timeout
- **CORS**: Handles cross-origin requests properly

### **Persistent Storage**
Settings are automatically saved to browser `localStorage`:
```javascript
{
  "apiUrl": "http://localhost:5000",
  "demoMode": "auto"
}
```

## 📱 Usage Examples

### **Ask a Safety Question**
1. Go to "Ask Questions" tab
2. Type: "What are the emergency evacuation procedures?"
3. Click "Ask Question"
4. View answer with citations and processing details

### **Start a Conversation**
1. Enter a conversation ID (e.g., "safety-training-session")
2. Ask: "What are the PPE requirements?"
3. Follow up: "How often should PPE be inspected?"
4. View conversation history below the response

### **Draft a Letter**
1. Go to "Draft Letters" tab  
2. Purpose: "Safety compliance reminder"
3. Add points: "Monthly inspection due", "PPE verification required"
4. Select tone: "Formal"
5. Click "Draft Letter" to generate professional correspondence

### **Monitor System Performance**
1. Go to "Metrics" tab
2. View real-time request counts, token usage, response times
3. Click "Refresh Metrics" for latest data
4. Monitor system health in the status bar

## 🔍 Technical Details

### **Frontend Architecture**
- **Single Page Application**: No server-side dependencies
- **Vanilla JavaScript**: No frameworks required
- **CSS Grid & Flexbox**: Modern, responsive layout
- **Async/Await**: Modern JavaScript for API calls

### **API Integration**
```javascript
// Example API call
async function apiRequest(endpoint, options = {}) {
    const response = await fetch(`${config.apiUrl}${endpoint}`, {
        headers: {
            'Content-Type': 'application/json',
            'X-Correlation-ID': generateCorrelationId()
        },
        ...options
    });
    
    return await response.json();
}
```

### **Error Handling**
- **Network Errors**: Graceful handling of API unavailability
- **Validation Errors**: Client-side and server-side validation feedback
- **Timeout Handling**: Automatic timeout for long-running requests
- **User Feedback**: Clear error messages and recovery suggestions

### **Performance Optimization**
- **Lazy Loading**: Metrics loaded only when tab is accessed
- **Debounced Requests**: Prevents rapid-fire API calls
- **Local Caching**: Settings cached in browser storage
- **Efficient DOM Updates**: Minimal DOM manipulation for smooth performance

## 🛠️ Customization

### **Styling**
The interface uses CSS custom properties (variables) for easy theming:
```css
:root {
    --primary-color: #667eea;
    --secondary-color: #764ba2;
    --success-color: #48bb78;
    --error-color: #f56565;
}
```

### **Adding New Features**
1. **Add Tab**: Create new tab button and content area
2. **API Integration**: Use existing `apiRequest()` function
3. **Form Handling**: Follow existing form submission patterns
4. **Error Handling**: Use `showError()` and `showSuccess()` functions

## 🔒 Security Considerations

- **No Sensitive Data Storage**: Passwords or API keys not stored in browser
- **XSS Prevention**: Proper HTML escaping for user input
- **CORS Compliance**: Respects API CORS policies
- **Input Validation**: Client-side validation before API calls

## 📊 Browser Compatibility

- ✅ **Chrome**: 80+ (recommended)
- ✅ **Firefox**: 75+
- ✅ **Safari**: 13+
- ✅ **Edge**: 80+

### **Required Browser Features**
- ES6+ JavaScript (async/await, fetch API)
- CSS Grid and Flexbox support
- LocalStorage API
- Modern DOM APIs

---

**The OHS Copilot web frontend provides an intuitive, professional interface for interacting with the enterprise AI safety assistant.**
