﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NatuurlikBase.Data;
using NatuurlikBase.Models;
using NatuurlikBase.Repository.IRepository;
using System.Security.Claims;

namespace NatuurlikBase.Controllers;
[Authorize(Roles = SR.Role_Admin)]
public class InventoryItemController : Controller
{

  private readonly DatabaseContext db;
    private readonly IUnitOfWork _unitOfWork;
    public InventoryItemController(DatabaseContext context , IUnitOfWork unitOfWork)
    {
        db = context;
        _unitOfWork = unitOfWork;
    }

    // GET: Countries
    public IActionResult Index()
    {
        var databaseContext = db.InventoryItem.Include(c => c.InventoryType);
        return View( databaseContext.ToList()); 
    }

 
    public IActionResult Create()
    {
        ViewData["InventoryTypeId"] = new SelectList(db.InventoryType, "Id", "InventoryTypeName");
  
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,InventoryItemName,InventoryTypeId,QuantityOnHand,ThresholdValue")] InventoryItem inventoryItem)
    {
        if (ModelState.IsValid)

        {

            if (db.InventoryItem.Any(c => c.InventoryItemName == inventoryItem.InventoryItemName && c.InventoryTypeId == inventoryItem.InventoryTypeId && c.QuantityOnHand == inventoryItem.QuantityOnHand
            && c.ThresholdValue == inventoryItem.ThresholdValue))
            {
                ViewBag.Error = "Inventory Type Already exist in the database.";
            }
            else
            {
                var claimsId = (ClaimsIdentity)User.Identity;
                var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
                var user = _unitOfWork.User.GetFirstOrDefault(x => x.Id == claim.Value);
                var fullName = user.FirstName + " " + user.Surname;
                var userName = fullName.ToString();

                db.InventoryItem.Add(inventoryItem);

                ViewBag.CountryConfirmation = "Are you sure you want to add a return reason.";
                await db.SaveChangesAsync(userName);
                TempData["success"] = "Inventory Item created successfully.";
                return RedirectToAction("Index");
            }

        }

        ViewData["InventoryTypeId"] = new SelectList(db.InventoryType, "Id", "InventoryTypeName", inventoryItem.InventoryTypeId);
        return View(inventoryItem);
    }

 
    public IActionResult Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }
        InventoryItem inventoryItem = db.InventoryItem.Find(id);
        if (inventoryItem == null)
        {
            return NotFound();
        }
        ViewData["InventoryTypeId"] = new SelectList(db.InventoryType, "Id", "InventoryTypeName", inventoryItem.InventoryTypeId);
    
        return View(inventoryItem);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([Bind("Id,InventoryItemName,InventoryTypeId,QuantityOnHand,ThresholdValue")] InventoryItem inventoryItem)
    {
        if (ModelState.IsValid)

        {
                if (db.InventoryItem.Any(c => c.InventoryItemName==inventoryItem.InventoryItemName &&c.InventoryTypeId==inventoryItem.InventoryTypeId && c.QuantityOnHand==inventoryItem.QuantityOnHand
                && c.ThresholdValue == inventoryItem.ThresholdValue))
            {
                ViewBag.Error = "Already exist in the database.";

            }
            else
            {
                var claimsId = (ClaimsIdentity)User.Identity;
                var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
                var user = _unitOfWork.User.GetFirstOrDefault(x => x.Id == claim.Value);
                var fullName = user.FirstName + " " + user.Surname;
                var userName = fullName.ToString();

                //db.Entry(inventoryItem).State = EntityState.Modified;
                _unitOfWork.InventoryItem.Update(inventoryItem);
                ViewBag.ReturnReasonConfirmation = "Are you sure with your return reason changes.";
                await db.SaveChangesAsync(userName);
                TempData["success"] = "Inventory Item Updated Successfully.";
                return RedirectToAction("Index");
            }
        }
        ViewData["InventoryTypeId"] = new SelectList(db.InventoryType, "Id", "InventoryTypeName", inventoryItem.InventoryTypeId);
        return View(inventoryItem);
    }

    public IActionResult Delete(int? id)
    {
        if (id == null)
        {

            return NotFound();
        }
     //   InventoryItem inventoryItem = db.InventoryItem.Find(id);
        var inventoryItem =db.InventoryItem
         .Include(c => c.InventoryType)
         .FirstOrDefault(m => m.Id == id);
        if (inventoryItem == null)
        {
            return NotFound();
        }
        return View(inventoryItem);

    }


    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        InventoryItem inventoryItem = db.InventoryItem.Find(id);
        db.InventoryItem.Remove(inventoryItem);
        ViewBag.CountryConfirmation = "Are you sure you want to delete a country.";
        var hasFk = _unitOfWork.InventoryProcured.GetAll().Any(x => x.Id == id);

        if (!hasFk)
        {
            var obj = _unitOfWork.InventoryItem.GetFirstOrDefault(u => u.Id == id);
            if (obj == null)
            {
                TempData["AlertMessage"] = "Error occurred while attempting delete";
            }
            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var user = _unitOfWork.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = user.FirstName + " " + user.Surname;
            var userName = fullName.ToString();

            _unitOfWork.InventoryItem.Remove(obj);
            await db.SaveChangesAsync(userName);
            TempData["success"] = "Inventory Item Successfully Deleted.";
            return RedirectToAction("Index");
        }
        else
        {
            TempData["Delete"] = "Inventory Item cannot be deleted since it has a Procured Inventory associated";
            return RedirectToAction("Index");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            db.Dispose();
        }
        base.Dispose(disposing);
    }
}

