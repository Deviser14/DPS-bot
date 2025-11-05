using DPS_bot.Models;
using DPS_bot.Services;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
// Заменяем SeleniumExtras на актуальный пакет
using SeleniumExtras.WaitHelpers;
using System;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        
        DpsParser parser = new DpsParser();
        parser.Parse("https://sirus.su/base/pve-progression/boss-kill/x3/64814");
    }
}