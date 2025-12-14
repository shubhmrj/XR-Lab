using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SimpleAuthController : MonoBehaviour
{
    void Start()
    {
        // Initialize simple auth system
        InitializeAuth();
    }
    // Panels
    public GameObject loginPanel;
    public GameObject signupPanel;
    public GameObject forgetPasswordPanel;
    public GameObject notificationPanel;
    public GameObject profilePanel;

    // TMP Input Fields
    public TMP_InputField loginEmail;
    public TMP_InputField loginPassword;
    public TMP_InputField signupEmail;
    public TMP_InputField signupPassword;
    public TMP_InputField signupCPassword;
    public TMP_InputField signUpUserName;
    public TMP_InputField forgetPassEmail;

    // Notification texts
    public TMP_Text notif_Title_Text;
    public TMP_Text notif_message_Text;
    public TMP_Text profileUserName_Text;
    public TMP_Text profileUserEmail_Text;
    public Toggle rememberme;

    // Simple authentication credentials for testing
    private Dictionary<string, UserData> registeredUsers = new Dictionary<string, UserData>();
    private UserData currentUser = null;
    private bool authInitialized = false;
    
    [System.Serializable]
    public class UserData
    {
        public string email;
        public string password;
        public string username;
        
        public UserData(string email, string password, string username)
        {
            this.email = email;
            this.password = password;
            this.username = username;
        }
    }


    // Open Login Panel
    public void OpenLoginPanel()
    {
        if (loginPanel != null && signupPanel != null && forgetPasswordPanel != null)
        {
            loginPanel.SetActive(true);
            signupPanel.SetActive(false);
            forgetPasswordPanel.SetActive(false);
            if (profilePanel != null) profilePanel.SetActive(false);
        }
    }

    // Open Signup Panel
    public void OpenSignupPanel()
    {
        if (loginPanel != null && signupPanel != null && forgetPasswordPanel != null)
        {
            loginPanel.SetActive(false);
            signupPanel.SetActive(true);
            forgetPasswordPanel.SetActive(false);
            if (profilePanel != null) profilePanel.SetActive(false);
        }
    }

    // Open Forget Password Panel
    public void OpenForgetPassPanel()
    {
        if (loginPanel != null && signupPanel != null && forgetPasswordPanel != null)
        {
            loginPanel.SetActive(false);
            signupPanel.SetActive(false);
            forgetPasswordPanel.SetActive(true);
            if (profilePanel != null) profilePanel.SetActive(false);
        }
    }
    
    // Open Profile Panel
    public void OpenProfilePanel()
    {
        if (loginPanel != null && signupPanel != null && forgetPasswordPanel != null && profilePanel != null)
        {
            loginPanel.SetActive(false);
            signupPanel.SetActive(false);
            forgetPasswordPanel.SetActive(false);
            profilePanel.SetActive(true);
        }
    }

    // Login User
    public void LoginUser()
    {
        Debug.Log("LoginUser() called");
        
        if (loginEmail == null)
        {
            Debug.LogError("loginEmail is null! Check Inspector assignment.");
            showNotificationMessage("Error", "Login email field not configured");
            return;
        }
        
        if (loginPassword == null)
        {
            Debug.LogError("loginPassword is null! Check Inspector assignment.");
            showNotificationMessage("Error", "Login password field not configured");
            return;
        }

        Debug.Log($"Email field text: '{loginEmail.text}', Password field text length: {loginPassword.text.Length}");

        if (string.IsNullOrEmpty(loginEmail.text) || string.IsNullOrEmpty(loginPassword.text))
        {
            showNotificationMessage("Error", "Login fields cannot be empty");
            return;
        }
        
        if (!authInitialized)
        {
            showNotificationMessage("Error", "Authentication system is still initializing. Please wait.");
            return;
        }

        SignInUser(loginEmail.text, loginPassword.text);
    }

    // Signup User
    public void SignUpUser()
    {
        Debug.Log("SignUpUser() called");
        
        if (signupEmail == null)
        {
            Debug.LogError("signupEmail is null! Check Inspector assignment.");
            showNotificationMessage("Error", "Signup email field not configured");
            return;
        }
        
        if (signupPassword == null)
        {
            Debug.LogError("signupPassword is null! Check Inspector assignment.");
            showNotificationMessage("Error", "Signup password field not configured");
            return;
        }
        
        if (signupCPassword == null)
        {
            Debug.LogError("signupCPassword is null! Check Inspector assignment.");
            showNotificationMessage("Error", "Confirm password field not configured");
            return;
        }
        
        if (signUpUserName == null)
        {
            Debug.LogError("signUpUserName is null! Check Inspector assignment.");
            showNotificationMessage("Error", "Username field not configured");
            return;
        }

        if (string.IsNullOrEmpty(signupEmail.text) || string.IsNullOrEmpty(signupPassword.text) || 
            string.IsNullOrEmpty(signupCPassword.text) || string.IsNullOrEmpty(signUpUserName.text))
        {
            showNotificationMessage("Error", "All signup fields are required");
            return;
        }

        if (signupPassword.text != signupCPassword.text)
        {
            showNotificationMessage("Error", "Passwords do not match");
            return;
        }
        
        if (signupPassword.text.Length < 6)
        {
            showNotificationMessage("Error", "Password must be at least 6 characters");
            return;
        }
        
        if (!authInitialized)
        {
            showNotificationMessage("Error", "Authentication system is still initializing. Please wait.");
            return;
        }

        CreateUser(signupEmail.text, signupPassword.text, signUpUserName.text);
    }

    // Forget Password
    public void ForgetPass()
    {
        Debug.Log("ForgetPass() called");
        
        if (forgetPassEmail == null)
        {
            Debug.LogError("forgetPassEmail is null! Check Inspector assignment.");
            showNotificationMessage("Error", "Forget password email field not configured");
            return;
        }

        if (string.IsNullOrEmpty(forgetPassEmail.text))
        {
            showNotificationMessage("Error", "Email field cannot be empty");
            return;
        }
        
        // For testing purposes, just show a message
        showNotificationMessage("Info", "Password reset is not available in test mode. Use test credentials: ritikrai4152@gmail.com / password123");
    }

    // Show Notification
    private void showNotificationMessage(string title, string message)
    {
        if (notif_Title_Text != null && notif_message_Text != null && notificationPanel != null)
        {
            notif_Title_Text.text = title;
            notif_message_Text.text = message;
            notificationPanel.SetActive(true);
        }
    }

    // Close Notification
    public void CloseNotif_panel()
    {
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }
    }

    public void LogOut()
    {
        // Clear current user
        currentUser = null;
        
        if (profileUserEmail_Text != null) profileUserEmail_Text.text = "";
        if (profileUserName_Text != null) profileUserName_Text.text = "";
        
        // Clear input fields
        if (loginEmail != null) loginEmail.text = "";
        if (loginPassword != null) loginPassword.text = "";
        
        OpenLoginPanel();
    }

    void CreateUser(string email, string password, string UserName)
    {
        // Check if user already exists
        if (registeredUsers.ContainsKey(email))
        {
            showNotificationMessage("Signup Failed", "This email is already registered");
            return;
        }
        
        // Create new user
        UserData newUser = new UserData(email, password, UserName);
        registeredUsers[email] = newUser;
        currentUser = newUser;
        
        Debug.Log($"User created successfully: {UserName} ({email})");
        
        // Update profile UI
        if (profileUserName_Text != null)
            profileUserName_Text.text = UserName;
        if (profileUserEmail_Text != null)
            profileUserEmail_Text.text = email;
            
        showNotificationMessage("Success", "Account created successfully!");
        OpenProfilePanel();
    }

    public void SignInUser(string email, string password)
    {
        // Check for test credentials first
        if (email == "ritikrai4152@gmail.com" && password == "password123")
        {
            currentUser = new UserData(email, password, "Ritik Rai");
            
            if (profileUserName_Text != null)
                profileUserName_Text.text = "Ritik Rai";
            if (profileUserEmail_Text != null)
                profileUserEmail_Text.text = email;
                
            showNotificationMessage("Success", "Welcome back, Ritik!");
            OpenProfilePanel();
            return;
        }
        
        // Check registered users
        if (registeredUsers.ContainsKey(email))
        {
            UserData user = registeredUsers[email];
            if (user.password == password)
            {
                currentUser = user;
                
                if (profileUserName_Text != null)
                    profileUserName_Text.text = user.username;
                if (profileUserEmail_Text != null)
                    profileUserEmail_Text.text = user.email;
                    
                showNotificationMessage("Success", $"Welcome back, {user.username}!");
                OpenProfilePanel();
            }
            else
            {
                showNotificationMessage("Login Failed", "Incorrect password");
            }
        }
        else
        {
            showNotificationMessage("Login Failed", "No account found with this email. Try ritikrai4152@gmail.com / password123");
        }
    }

    void InitializeAuth()
    {
        Debug.Log("Initializing simple authentication system...");
        
        // Add test user to registered users for convenience
        registeredUsers["ritikrai4152@gmail.com"] = new UserData("ritikrai4152@gmail.com", "password123", "Ritik Rai");
        
        authInitialized = true;
        Debug.Log("Simple authentication system initialized successfully!");
        Debug.Log("Test credentials: ritikrai4152@gmail.com / password123");
    }

}
