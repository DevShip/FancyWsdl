using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace FancyWsdl;

public class GostHttpClient
{
    public readonly string CertificateSerial;

     
    private readonly HttpClient _http;

    public GostHttpClient(string certificateSerial, IWebProxy proxy = null)
    {
        CertificateSerial = certificateSerial;

        var cert = FindCertificate(certificateSerial, StoreName.My, StoreLocation.CurrentUser);
        if (cert == null)
        {
            throw new ArgumentOutOfRangeException(nameof(certificateSerial));
        }

        var handler = new HttpClientHandler();
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.SslProtocols = SslProtocols.Tls12;
        handler.ClientCertificates.Add(cert);
        handler.Proxy = proxy;
        handler.UseProxy = false;
        handler.ServerCertificateCustomValidationCallback += ServerCertificateCustomValidationCallback;

        _http = new HttpClient(handler);
    }

    private bool ServerCertificateCustomValidationCallback(HttpRequestMessage arg1, X509Certificate2 arg2, X509Chain arg3, SslPolicyErrors arg4)
    {
        if (arg4 != SslPolicyErrors.None)
        {
            Console.WriteLine($"Не удалось проверить цепочку сертификатов проверьте сертификаты сервера!!!");
        }
        return true;
    }

    /// <summary>
    /// Скачать схему сервиса Wsdl в файл
    /// </summary>
    public  bool DownloadWsdl(string url, string fileName, CancellationToken cancellationToken)
    {
         
        var requestMsg = new HttpRequestMessage(HttpMethod.Get, url);

        var hresp =  _http.SendAsync(requestMsg, cancellationToken);
        hresp.Wait(30);
        var resp = hresp?.Result;


        if (resp?.Content != null)
        {
               
            File.WriteAllText(fileName,  resp.Content.ReadAsStringAsync().Result);
            return true;
        }

        return false;
    }

   

    /// <summary>
    /// For the unit tests, set this to the StoreLocation.CurrentUser.
    /// For the production code, keep it set to the StoreLocation.LocalMachine.
    /// Only Administrator or LocalSystem accounts can access the LocalMachine stores.
    /// </summary>
    public static StoreLocation DefaultStoreLocation = StoreLocation.LocalMachine;

    /// <summary>
    /// Looks for the GOST certificate with a private key using the subject name or a thumbprint.
    /// Returns null, if certificate is not found, the algorithm isn't GOST-compliant, or the private key is not associated with it.
    /// </summary>
    public static X509Certificate2 FindCertificate(string cnameOrThumbprintOrSerial,
        StoreName storeName = StoreName.My, StoreLocation? storeLocation = StoreLocation.CurrentUser)
    {
        // avoid returning any certificate
        if (string.IsNullOrWhiteSpace(cnameOrThumbprintOrSerial))
        {
            return null;
        }

        // a thumbprint is a hexadecimal number, compare it case-insensitive
        using var store = new X509Store(storeName, storeLocation ?? DefaultStoreLocation);

        store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

        foreach (var certificate in store.Certificates)
        {
            if (certificate.HasPrivateKey)
            {
                if (certificate.SubjectName.Name != null &&
                    certificate.SubjectName.Name.IndexOf(cnameOrThumbprintOrSerial,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    return certificate;

                if (StringComparer.OrdinalIgnoreCase.Equals(certificate.SerialNumber, cnameOrThumbprintOrSerial))
                    return certificate;

                if (StringComparer.OrdinalIgnoreCase.Equals(certificate.Thumbprint, cnameOrThumbprintOrSerial))
                    return certificate;
            }
        }

        return null;
    }
}