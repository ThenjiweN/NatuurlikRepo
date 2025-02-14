﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NatuurlikBase.Data;
using NatuurlikBase.Models;
using NatuurlikBase.Repository.IRepository;
using NatuurlikBase.ViewModels;
using PhoneNumbers;
using System.Security.Claims;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NatuurlikBase.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly DatabaseContext _db;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IConfiguration _configuration;

        [BindProperty]
        public OrderVM OrderVM { get; set; }
        public OrderController(IUnitOfWork uow, DatabaseContext db,IWebHostEnvironment hostEnvironment, IEmailSender emailSender, IConfiguration configuration)
        {
            _uow = uow;
            _db = db;
            _hostEnvironment = hostEnvironment;
            _emailSender = emailSender;
            _configuration = configuration;
        }

        public ActionResult GetQueryReasons(int queryReasonId)
        {
            return Json(_uow.QueryReason.GetAll(x => x.Id == queryReasonId).Select(x => new
            {
                Text = x.Name,
                Value = x.Id
            }).OrderBy(x => x.Text).ToList());
        }


        public IActionResult Detail(int? orderId)
        {

            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null)
            {
                var hasCart = _uow.UserCart.GetAll(x => x.ApplicationUserId == claim.Value).FirstOrDefault();
                ViewData["has"] = hasCart;
            }

            //Load all order details
            OrderVM orderVM = new OrderVM()
            {
                Order = _uow.Order.GetFirstOrDefault(u => u.Id == orderId, includeProperties: "ApplicationUser,Country,Province,City,Suburb,Courier"),
                OrderLine = _uow.OrderLine.GetAll(ol => ol.OrderId == orderId, includeProperties: "Product")
            };

            var orderprsn = _db.Order.Where(z => z.Id == orderId && z.OrderPaymentStatus == "Payment Outstanding" && z.OrderStatus != "Cancelled"
            && z.OrderStatus != "Rejected" && z.OrderStatus != "Pending").FirstOrDefault();
            if (orderprsn != null)
            {
                var fullTime = _db.PaymentReminder.FirstOrDefault(x => x.Id == orderprsn.PaymentReminderId).Value;
                var orderProcDate = orderprsn.ProcessedDate.Date;
                var threshold = orderProcDate.AddDays(fullTime);
                string fullDate = threshold.ToString("D");
                var remaining = threshold - DateTime.Today;
                var remainingDate = remaining.ToString("dd");
                TempData["Remaining"] = remainingDate;
                TempData["Due"] = fullDate;
            }

            var overdue = _db.Order.Where(z => z.Id == orderId && z.OrderPaymentStatus == "Payment Overdue" && z.OrderStatus != "Cancelled"
            && z.OrderStatus != "Rejected" && z.OrderStatus != "Pending").FirstOrDefault();
            if (overdue != null)
            {
                var fullTime = _db.PaymentReminder.FirstOrDefault(x => x.Id == overdue.PaymentReminderId).Value;
                var orderProcDate = overdue.ProcessedDate.Date;
                var threshold = orderProcDate.AddDays(fullTime);
                string fullDate = threshold.ToString("D");
                var overDue = DateTime.Today - threshold;
                var dateOver = overDue.ToString("dd");
                TempData["Due"] = fullDate;
                TempData["OverDue"] = dateOver;
            }

            return View(orderVM);
        }

        [HttpPost]
        public async Task<IActionResult> AutoSavePackageOrder(int OrderlineId, bool IsChecked)
        {
            var obj = _db.OrderLine.Where(x => x.Id == OrderlineId).FirstOrDefault();
            //Save changes
            obj.IsPackaged = IsChecked;
            _db.SaveChanges();

            TempData["Success"] = "Product packaged successfully.";
            return RedirectToAction("Detail", "Order", new { orderId = obj.OrderId });

        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveResellerOrder()
        {
            //Retrieve Order details from the db
            var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
            //Update order status to approved state and save changes to db.
            _uow.Order.UpdateOrderStatus(OrderVM.Order.Id, SR.ProcessingOrder);

            orderRetrieved.ProcessedDate = DateTime.Now;
            _uow.Save();

            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
            var userName = fullName.ToString();

            await _db.SaveChangesAsync(userName);

            var user = _db.User.Where(z => z.Id == orderRetrieved.ApplicationUserId).FirstOrDefault();
            string email = user.Email;
            string name = user.FirstName;
            string number = orderRetrieved.Id.ToString();
            string date = orderRetrieved.CreatedDate.ToString("M");
            var callbackUrl = Url.Action("Index", "Order", values: null, protocol: Request.Scheme);

            string wwwRootPath = _hostEnvironment.WebRootPath;
            var template = System.IO.File.ReadAllText(Path.Combine(wwwRootPath, @"emailTemp\appOrderTemp.html"));
            template = template.Replace("[NAME]", name)
                .Replace("[ID]", number).Replace("[DATE]", date).Replace("[URL]", callbackUrl);
            string message = template;

             await _emailSender.SendEmailAsync(
            email,
            "Order Approved",
            message);

            TempData["Success"] = "Reseller Order has been approved successfully.";
            return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });
          
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder()
        {
            //Retrieve Order details from the db
            var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
            //Update order status to approved state and save changes to db.
            _uow.Order.UpdateOrderStatus(OrderVM.Order.Id, SR.OrderCancelled);
            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
            var userName = fullName.ToString();
            await _db.SaveChangesAsync(userName);

            var user = _db.User.Where(z => z.Id == orderRetrieved.ApplicationUserId).FirstOrDefault();
            string email = user.Email;
            string name = user.FirstName;
            string number = orderRetrieved.Id.ToString();
            string date = orderRetrieved.CreatedDate.ToString("M");
            var callbackUrl = Url.Action("Index", "Order", values: null, protocol: Request.Scheme);

            string wwwRootPath = _hostEnvironment.WebRootPath;
            var template = System.IO.File.ReadAllText(Path.Combine(wwwRootPath, @"emailTemp\canResOrderTemp.html"));
            template = template.Replace("[NAME]", name)
                .Replace("[ID]", number).Replace("[DATE]", date).Replace("[URL]", callbackUrl);
            string message = template;

            await _emailSender.SendEmailAsync(
            email,
            "Order Cancelled",
            message);

            TempData["Success"] = "Order has been cancelled successfully.";
            return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });

        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult BacklogOrder()
        //{
        //    //Retrieve Order details from the db
        //    var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
        //    //Update order status to approved state and save changes to db.
        //    _uow.Order.UpdateOrderStatus(OrderVM.Order.Id, SR.OrderDelayed);
        //    orderRetrieved.BackOrderDate = DateTime.Now;
        //    _uow.Save();

        //    var user = _db.User.Where(z => z.Id == orderRetrieved.ApplicationUserId).FirstOrDefault();
        //    var fullTime = _db.ConfirmationReminder.FirstOrDefault(x => x.Id == orderRetrieved.ConfirmationReminderId).Value;
        //    var orderDate = orderRetrieved.BackOrderDate.Date;
        //    var threshold = orderDate.AddDays(fullTime);
        //    string email = user.Email;
        //    string name = user.FirstName;
        //    string number = orderRetrieved.Id.ToString();
        //    string date = DateTime.Now.ToString("M");
        //    string fullDate = threshold.ToString("D");
        //    string status = orderRetrieved.OrderPaymentStatus.ToString();
        //    var callbackUrl = Url.Action("Index", "Order", values: null, protocol: Request.Scheme);

        //    string wwwRootPath = _hostEnvironment.WebRootPath;
        //    var template = System.IO.File.ReadAllText(Path.Combine(wwwRootPath, @"emailTemp\modOrdTemp.html"));
        //    template = template.Replace("[NAME]", name).Replace("[STATUS]", status).Replace("[DUE]", fullDate)
        //        .Replace("[ID]", number).Replace("[DATE]", date).Replace("[URL]", callbackUrl);
        //    string message = template;

        //    _emailSender.SendEmailAsync(
        //    email,
        //    "Order Delayed - Pending Confirmation",
        //    message);

        //    TempData["Success"] = "Order added to orders backlog successfully.";
        //    return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });

        //}

        //Reseller accepts delayed order.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult ConfirmOrder()
        //{
        //    //Retrieve Order details from the db
        //    var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
        //    //Update order status to approved state and save changes to db.
        //    _uow.Order.UpdateOrderStatus(OrderVM.Order.Id, SR.ProcessingOrder);
        //    _uow.Save();

        //    var user = _db.User.Where(z => z.Id == orderRetrieved.ApplicationUserId).FirstOrDefault();
        //    string email = user.Email;
        //    string name = user.FirstName;
        //    string number = orderRetrieved.Id.ToString();
        //    string date = orderRetrieved.CreatedDate.ToString("M");
        //    string status = orderRetrieved.OrderPaymentStatus.ToString();
        //    var callbackUrl = Url.Action("Index", "Order", values: null, protocol: Request.Scheme);

        //    string wwwRootPath = _hostEnvironment.WebRootPath;
        //    var template = System.IO.File.ReadAllText(Path.Combine(wwwRootPath, @"emailTemp\ordConfTemp.html"));
        //    template = template.Replace("[NAME]", name).Replace("[STATUS]", status)
        //        .Replace("[ID]", number).Replace("[DATE]", date).Replace("[URL]", callbackUrl);
        //    string message = template;

        //    _emailSender.SendEmailAsync(
        //    email,
        //    "Order Confirmed",
        //    message);

        //    TempData["Success"] = "Your order has been confirmed successfully.";
        //    return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });

        //}

        //Reseller rejects delayed order.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult RejectOrder()
        //{
        //    //Retrieve Order details from the db
        //    var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
        //    //Update order status to approved state and save changes to db.
        //    _uow.Order.UpdateOrderStatus(OrderVM.Order.Id, SR.RejectDelayedOrder);
        //    _uow.Save();
        //    TempData["Success"] = "Your order has been rejected successfully.";
        //    return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });

        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessOrder()
        {
            //Retrieve Order details from the db
            var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
            //Update order status to approved state and save changes to db.
            _uow.Order.UpdateOrderStatus(OrderVM.Order.Id, SR.ProcessingOrder);
            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
            var userName = fullName.ToString();
            await _db.SaveChangesAsync(userName);
            TempData["Success"] = "Order status updated successfully.";
            return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectResellerOrder()
        {
            //Retrieve Order details from the db
            var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
            //Update order status to approved state and save changes to db.
            _uow.Order.UpdateOrderStatus(OrderVM.Order.Id, SR.OrderRejected);
            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
            var userName = fullName.ToString();
            await _db.SaveChangesAsync(userName);

            var user = _db.User.Where(z => z.Id == orderRetrieved.ApplicationUserId).FirstOrDefault();
            string email = user.Email;
            string name = user.FirstName;
            string number = orderRetrieved.Id.ToString();
            string date = orderRetrieved.CreatedDate.ToString("M");
            var callbackUrl = Url.Action("Index", "Order", values: null, protocol: Request.Scheme);

            string wwwRootPath = _hostEnvironment.WebRootPath;
            var template = System.IO.File.ReadAllText(Path.Combine(wwwRootPath, @"emailTemp\rejOrderTemp.html"));
            template = template.Replace("[NAME]", name)
                .Replace("[ID]", number).Replace("[DATE]", date).Replace("[URL]", callbackUrl);
            string message = template;

            await _emailSender.SendEmailAsync(
            email,
            "Order Rejected",
            message);

            TempData["Success"] = "Reseller Order has been rejected successfully.";
            return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CaptureResellerPayment()
        {
            //Retrieve Order details from the db
            var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
            //Update order status to approved state and save changes to db.
            _uow.Order.UpdateOrderPaymentStatus(OrderVM.Order.Id, SR.OrderPaymentApproved);
            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
            var userName = fullName.ToString();
            await _db.SaveChangesAsync(userName);

            var user = _db.User.Where(z => z.Id == orderRetrieved.ApplicationUserId).FirstOrDefault();
            string email = user.Email;
            string name = user.FirstName;
            string number = orderRetrieved.Id.ToString();
            string date = DateTime.Now.ToString("M");
            string status = orderRetrieved.OrderPaymentStatus.ToString();
            string total = orderRetrieved.OrderTotal.ToString();
            var callbackUrl = Url.Action("Index", "Order", values: null, protocol: Request.Scheme);

            string wwwRootPath = _hostEnvironment.WebRootPath;
            var template = System.IO.File.ReadAllText(Path.Combine(wwwRootPath, @"emailTemp\paidTemp.html"));
            template = template.Replace("[NAME]", name).Replace("[STATUS]", status)
                .Replace("[ID]", number).Replace("[DATE]", date).Replace("[TOTAL]", total).Replace("[URL]", callbackUrl);
            string message = template;

            await _emailSender.SendEmailAsync(
            email,
            "Payment Received",
            message);

            TempData["Success"] = "Reseller Order payment captured successfully.";
            return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });

        }

        public async Task<IActionResult> DispatchParcel()
        {
            //Retrieve Order details from the db
            var orderRetrieved = _uow.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
            //Capture required details in order to ship/dispatch parcel.
            orderRetrieved.ParcelTrackingNumber = OrderVM.Order.ParcelTrackingNumber;
            orderRetrieved.DispatchedDate = DateTime.Now;
            orderRetrieved.OrderStatus = SR.OrderDispatched;
            _uow.Order.Update(orderRetrieved);
            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
            var userName = fullName.ToString();
            await _db.SaveChangesAsync(userName);

            string accountId = _configuration["AccountId"];
            string authToken = _configuration["AuthToken"];
            TwilioClient.Init(accountId, authToken);
            var couriers = _uow.Courier.GetFirstOrDefault(z => z.Id == orderRetrieved.CourierId).CourierName;
            var name = orderRetrieved.FirstName;
            var order = orderRetrieved.Id;
            var track = orderRetrieved.ParcelTrackingNumber;
            var to = "+27" + orderRetrieved.PhoneNumber;
            var companyNr = "+18305216564";


            if (couriers == "Natuurlik Free Delivery")
            {
                var message = MessageResource.Create(
                    to,
                    from: companyNr,
                    body: $"Hi " + name + " your Natuurlik order #" + order + " has been dispatched, you will receieve a call once it is out for delivery");
            }
            else
            {
                var message = MessageResource.Create(
                    to,
                    from: companyNr,
                    body: $"Hi " + name + " your Natuurlik order #" + order + " has been dispatched, your tracking number is " + track);
            }

            TempData["Success"] = "Order status updated successfully.";
            return RedirectToAction("Detail", "Order", new { orderId = OrderVM.Order.Id });

        }
        public async Task<IActionResult> ViewQueries()

        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null)
            {
                var hasCart = _uow.UserCart.GetAll(x => x.ApplicationUserId == claim.Value).FirstOrDefault();
                ViewData["has"] = hasCart;
            }



            IEnumerable<OrderQuery> orderQueries;

            if (User.IsInRole(SR.Role_Admin) || User.IsInRole(SR.Role_SA))
            {
                orderQueries = _uow.OrderQuery.GetAll(includeProperties: "Order,QueryReason");
            }
            //Allow only for order to be queried which user placed
            else
            {
                orderQueries = _uow.OrderQuery.GetAll(x => x.Order.ApplicationUserId == claim.Value, includeProperties: "Order,QueryReason");
            }

            return View(orderQueries);
        }

        public IActionResult ViewReviews()

        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            IEnumerable<OrderReview> orderReview;

            if (User.IsInRole(SR.Role_Admin) || User.IsInRole(SR.Role_SA))
            {
                orderReview = _uow.OrderReview.GetAll(includeProperties: "order,ReviewReason");
            }
            //Allow only for order to be queried which user placed
            else
            {
                orderReview = _uow.OrderReview.GetAll(x => x.order.ApplicationUserId == claim.Value, includeProperties: "order,ReviewReason");
            }

            return View(orderReview);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateQuery(OrderQueryVM orderQueryVM)
        {
            orderQueryVM.OrderQuery.QueryStatus = SR.QueryLogged;
            _db.OrderQuery.Add(orderQueryVM.OrderQuery);
            _db.SaveChanges();
            TempData["success"] = "Your query has been submitted successfully.";
            return RedirectToAction("ViewQueries");

        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateReview(OrderReviewVMcs orderReviewVM)
        {
            //orderReviewVM.Order.QueryStatus = SR.QueryLogged;
            _db.OrderReview.Add(orderReviewVM.OrderReview);
            _db.SaveChanges();
            TempData["success"] = "Your Review has been submitted successfully.";
            return RedirectToAction("Index");

        }
        //GET
        public IActionResult ReviewQuery(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }

            var orderQuery = _uow.OrderQuery.GetFirstOrDefault(u => u.Id == id, includeProperties: "Order,QueryReason");


            if (orderQuery == null)
            {
                return NotFound();
            }

            return View(orderQuery);
        }

        //POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewQuery(OrderQuery orderQuery)

        {
            orderQuery.QueryStatus = SR.QueryReview;
            orderQuery.QueryFeedback = orderQuery.QueryFeedback;
            _db.OrderQuery.Update(orderQuery);
            var claimsId = (ClaimsIdentity)User.Identity;
            var claim = claimsId.FindFirst(ClaimTypes.NameIdentifier);
            var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
            var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
            var userName = fullName.ToString();
            await _db.SaveChangesAsync(userName);
            TempData["success"] = "The order query was reviewed successfully.";
            return RedirectToAction("ViewQueries");

        }


        [HttpGet]
        public async Task<IActionResult> LogQuery(int orderId)
        {

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null)
            {
                var hasCart = _uow.UserCart.GetAll(x => x.ApplicationUserId == claim.Value).FirstOrDefault();
                ViewData["has"] = hasCart;
            }

            if (User.IsInRole(SR.Role_Admin) || User.IsInRole(SR.Role_SA))
            {
                OrderQueryVM orderQueryVM = new()
                {
                    OrderQuery = new(),
                    OrdersList = _uow.Order.GetAll().Select(i => new SelectListItem
                    {
                        Text = i.Id.ToString(),
                        Value = i.Id.ToString()
                    }),
                    QueryReasons = _uow.QueryReason.GetAll().Select(i => new SelectListItem
                    {
                        Text = i.Name,
                        Value = i.Id.ToString()
                    })
                };

                _db.SaveChanges();

                return View(orderQueryVM);
            }
            else
            {
                //get the orders associated only with the customer or reseller.


                OrderQueryVM orderQueryVM = new()
                {
                    OrderQuery = new(),
                    OrdersList = _db.Order.Where(x => x.Id == orderId).Select(i => new SelectListItem
                    {
                        Text = i.Id.ToString(),
                        Value = i.Id.ToString()
                    }),
                    QueryReasons = _uow.QueryReason.GetAll().Select(i => new SelectListItem
                    {
                        Text = i.Name,
                        Value = i.Id.ToString()
                    })
                };
                orderQueryVM.OrderQuery.OrderId = orderId;
                ViewBag.Confirmation = "Confirm order query details?";
                var userRetrieved = _uow.User.GetFirstOrDefault(x => x.Id == claim.Value);
                var fullName = userRetrieved.FirstName + " " + userRetrieved.Surname;
                var userName = fullName.ToString();
                await _db.SaveChangesAsync(userName);

                return View(orderQueryVM);
            }

        }

        [HttpGet]
        public IActionResult LogReview(int orderId)
        {

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null)
            {
                var hasCart = _uow.UserCart.GetAll(x => x.ApplicationUserId == claim.Value).FirstOrDefault();
                ViewData["has"] = hasCart;
            }


            if (User.IsInRole(SR.Role_Admin) || User.IsInRole(SR.Role_SA))
            {
                OrderReviewVMcs orderReviewVM = new()
                {
                    OrderReview = new(),
                    OrdersList = _uow.Order.GetAll().Select(i => new SelectListItem
                    {
                        Text = i.Id.ToString(),
                        Value = i.Id.ToString()
                    }),
                    ReviewReasons = _uow.ReviewReason.GetAll().Select(i => new SelectListItem
                    {
                        Text = i.Name,
                        Value = i.Id.ToString()
                    })
                };

                _db.SaveChanges();

                return View(orderReviewVM);
            }
            else
            {
                //get the orders associated only with the customer or reseller.


                OrderReviewVMcs orderReviewVM = new()
                {
                    OrderReview = new(),
                    OrdersList = _db.Order.Where(x => x.Id == orderId).Select(i => new SelectListItem
                    {
                        Text = i.Id.ToString(),
                        Value = i.Id.ToString()
                    }),
                    ReviewReasons = _uow.ReviewReason.GetAll().Select(i => new SelectListItem
                    {
                        Text = i.Name,
                        Value = i.Id.ToString()
                    })
                };
                orderReviewVM.OrderReview.OrderId = orderId;
                ViewBag.Confirmation = "Confirm order query details?";
                _db.SaveChanges();

                return View(orderReviewVM);
            }

        }
        public IActionResult QueryDetail(int? queryId)
        {
            //Load all order details
            OrderQuery orderquery = _uow.OrderQuery.GetFirstOrDefault(x => x.Id == queryId, includeProperties: "Order,QueryReason");

            return View(orderquery);
        }

        public IActionResult Index(string status)
        {



            IEnumerable<Order> orders;

            if (User.IsInRole(SR.Role_Admin) || User.IsInRole(SR.Role_SA))
            {
                //Retrieve all orders for Administrator and Sales Assistant roles.
                 orders = _uow.Order.GetAll(x => x.OrderPaymentStatus != SR.CustomerPaymentPending, includeProperties: "ApplicationUser");
            }
            else
            {
                //get the orders associated only with the customer or reseller.
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                orders = _uow.Order.GetAll(u => u.ApplicationUserId == claim.Value && u.OrderPaymentStatus != SR.CustomerPaymentPending, includeProperties: "ApplicationUser");

                if (claim != null)
                {
                    var hasCart = _uow.UserCart.GetAll(x => x.ApplicationUserId == claim.Value).FirstOrDefault();
                    ViewData["has"] = hasCart;
                }


            }
           

            switch(status)
            {
                case "processing":
                    orders = orders.Where(u => u.OrderStatus == SR.ProcessingOrder);
                    break;

                case "pending":
                    orders = orders.Where(o => o.OrderStatus == SR.OrderPending);
                    break;

                case "dispatched":
                    orders = orders.Where(o => o.OrderStatus == SR.OrderDispatched);
                    break;

                case "cancelled":
                    orders = orders.Where(o => o.OrderStatus == SR.OrderCancelled);
                    break;

                case "refundpending":
                    orders = orders.Where(o => o.OrderStatus == SR.OrderRefundPending);
                    break;

                case "refunded":
                    orders = orders.Where(o => o.OrderStatus == SR.OrderRefunded);
                    break;

                default:
                    break;
            }
            return View(orders);
        }

       

    }
}
