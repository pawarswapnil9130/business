using System;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

class Program
{
    static void Main()
    {
        var account = new Account("Root", "434173469856813", "mSfTZPa4xV_wNMXxnFOl61iPBos");
        var cloudinary = new Cloudinary(account);
        try {
            var result = cloudinary.GetResource("test");
            Console.WriteLine("Success or got standard error: " + result.StatusCode);
        } catch (Exception ex) {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }
}
