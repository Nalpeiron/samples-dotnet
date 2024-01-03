This is a simple console application that demonstrates how you can integrate with the Zentitle2 Webhooks.

### Run the application on your localhost
1. Install [localtunnel](https://theboroer.github.io/localtunnel-www/) or some other tunneling service like [ngrok](https://ngrok.com/), which will expose the Webhooks console application to the internet.
    ```bash
    npm install -g localtunnel
    ```

2. Start the application executing following command in the **Webhooks.Console** directory.
    ```bash
    dotnet run
    ```

3. Expose the application to the internet using localtunnel and copy the URL returned by the command.
    ```bash
    lt --port 5003
    ```

### Configure and test the webhook
1. Login to Zentitle2 and navigate to **Account** -> **Webhooks**
2. Click **Add Webhook** and enter the URL received from the localtunnel.
3. Select the events you want to receive notifications for and click **Save**.
4. Click **Test** to send a test event to the Webhooks console application.
5. Check the console application output to verify that the event was received.

### Explore the webhooks' payload
According to the events you selected in the previous step, you can now test the Webhooks by performing the appropriate actions in Zentitle2 
(e.g. create a new entitlement, update it, activate the seat, create a customer, etc. ) and checking the console application output to see the payload received in the webhook's HTTP request's body.

### Verify the webhook's signature
Because the webhook URL is exposed to the internet, it is important to verify that the webhook HTTP request is coming 
from Zentitle2 and not from a malicious third party.
Zentitle2 signs the webhook's data with the secret key and includes the signature in the HTTP request's header.
The webhook consumer can then verify the authenticity and integrity of the received webhook's HTTP payload.
 
To enable the signature verification in the demo app, you need to modify the **appsettings.json** file as follows:
1. Set `ValidateSignature` to `true`
2. Copy the **RSA Modulus** value from the **Account** -> **Credentials** page in Zentitle2 and set it as the value of the `PublicKeyModulus` 

