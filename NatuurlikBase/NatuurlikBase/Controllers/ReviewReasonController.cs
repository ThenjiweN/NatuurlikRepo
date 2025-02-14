﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NatuurlikBase.Data;
using NatuurlikBase.Models;
using NatuurlikBase.Repository.IRepository;
using System.Security.Claims;

namespace NatuurlikBase.Controllers;

[Authorize(Roles = SR.Role_Admin)]
public class ReviewReasonController : Controller
{
    private readonly DatabaseContext db;
    private readonly IUnitOfWork _uow;

    public ReviewReasonController(DatabaseContext context, IUnitOfWork uow)
    {
        db = context;
        _uow = uow;
    }

    // GET: Countries
    public IActionResult Index()
    {
        return View(db.ReviewReason.ToList());
    }

    // GET: Countries/Details/5
    public IActionResult Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }
        ReviewReason ReviewReason = db.ReviewReason.Find(id);
        if (ReviewReason == null)
        {
            return NotFound();
        }
        return View(ReviewReason);
    }

    // GET: Countries/Create
    public IActionResult Create()
    {
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name")] ReviewReason reviewReason)
    {
        if (ModelState.IsValid)

        {
            if (db.ReviewReason.Any(c => c.Name.Equals(reviewReason.Name)))
            {
                ViewBag.ReturnError = "Review Reason Already exist in the database.";


            }
            else
            {
                db.ReviewReason.Add(reviewReason);

                ViewBag.ReviewReasonConfirmation = "Are you sure you want to add a Review reason.";
                var claimsId = (ClaimsIdentity)User.Identity;
                var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
                var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
                var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
                var userName = fullName.ToString();
                await db.SaveChangesAsync(userName);

                TempData["success"] = "Review Reason created successfully.";
                return RedirectToAction("Index");
            }

        }

        else if (!ModelState.IsValid)

        {
            ViewBag.modal = "invalid.";

        }
        return View(reviewReason);
    }


    public IActionResult Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }
        ReviewReason reviewReason = db.ReviewReason.Find(id);
        if (reviewReason == null)
        {
            return NotFound();
        }
        return View(reviewReason);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([Bind("Id,Name")] ReviewReason reviewReason)
    {
        if (ModelState.IsValid)

        {

            if (db.ReviewReason.Any(c => c.Name.Equals(reviewReason.Name)))
            {
                ViewBag.Error = "Review Reason Already exist in the database.";

            }
            else
            {
                //db.Entry(ReviewReason).State = EntityState.Modified;
                _uow.ReviewReason.Update(reviewReason);
                TempData["success"] = "Review Reason Successfully Edited.";
                ViewBag.ReviewReasonConfirmation = "Are you sure with your Review reason changes.";
                var claimsId = (ClaimsIdentity)User.Identity;
                var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
                var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
                var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
                var userName = fullName.ToString();
                await db.SaveChangesAsync(userName);
                return RedirectToAction("Index");
            }
        }
        return View(reviewReason);
    }


    // GET: ReviewReason/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var ReviewReason = await db.ReviewReason
            .FirstOrDefaultAsync(m => m.Id == id);
        if (ReviewReason == null)
        {
            return NotFound();
        }

        return View(ReviewReason);
    }

    // POST: WriteOffReason/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        ReviewReason ReviewReason = db.ReviewReason.Find(id);
        db.ReviewReason.Remove(ReviewReason);

        ViewBag.ReviewReasonConfirmation = "Are you sure you want to delete this Review Reason";

        var ForeignKey = db.OrderReview.Any(x => x.ReviewReasonId == id);
        if (!ForeignKey)
        {
            var obj = db.OrderReview.FirstOrDefault(x => x.ReviewReasonId == id);
            if (obj == null)
            {
                TempData["AlertMessage"] = "Oops! This ReviewReason cannot be deleted!";
            }
            db.ReviewReason.Remove(ReviewReason);
            TempData["success"] = "Review Reason Deleted Successfully";
            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
            var userName = fullName.ToString();
            await db.SaveChangesAsync(userName);
            return RedirectToAction("Index");
        }
        else
        {
            TempData["Delete"] = "ReviewReason cannot be deleted since it has an Order Review associated";
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

