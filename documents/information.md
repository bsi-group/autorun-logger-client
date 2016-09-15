# AutoRun Logger Client

## Process
The Windows client autorun data is sent via a HTTPS (TLS) service to the server. The client uses the **server.pem** file as a certificate pinning mechanism, this allows the use of a self signed TLS certificate.

The client connects to the HTTPS port and sends data to the server. The request URL takes the form of:

https://1.2.3.4:8000/domain/host/user

All data sent from the client is compressed using GZIP.
