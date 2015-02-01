using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.AccessControl;
using System.Web;
using System.Web.Mvc;
using AlphaCamp.Models;
using Microsoft.AspNet.Identity;

namespace AlphaCamp.Controllers
{
    [Authorize]
    public class PeopleController : Controller
    {
        lonelyeAGTyCH6mBEntities db = new lonelyeAGTyCH6mBEntities();

        // GET: People
        public ActionResult Index()
        {
            var types = db.FoodTypes.ToList();
            var items = new List<SelectListItem>();
            foreach (var item in types)
            {
                items.Add(new SelectListItem()
                {
                    Text = item.Name,
                    Value = item.Id
                });
            }
            ViewBag.foodTypes = items;
            return View();
        }

        [HttpPost]
        public ActionResult Index(Appointment model)
        {
            if (ModelState.IsValid)
            {
                model.Id = Guid.NewGuid().ToString().ToUpper();
                model.CreatTimeUtc = DateTime.UtcNow;
                if (User.Identity.IsAuthenticated)
                    model.CreaterId = User.Identity.GetUserId();
                db.Appointments.Add(model);
                db.SaveChanges();

                TempData["alert"] = "活動成功建立";
                return RedirectToAction("YesMan", new { id = model.Id });
            }

            var types = db.FoodTypes.ToList();
            var items = new List<SelectListItem>();
            foreach (var item in types)
            {
                items.Add(new SelectListItem()
                {
                    Text = item.Name,
                    Value = item.Id
                });
            }
            ViewBag.foodTypes = items;

            string alert = string.Empty;
            foreach (ModelState modelState in ViewData.ModelState.Values) {
                foreach (ModelError error in modelState.Errors)
                {
                    alert += error.ErrorMessage + "<br/>";
                }
            }

            TempData["alert"] = alert;

            return View(model);
        }

        public ActionResult All()
        {
            return View();
        }

        public JsonResult GetAllAppointment()
        {
            var appoints = db.Appointments.AsQueryable();
            //string userId = User.Identity.GetUserId();
            //if (User.Identity.IsAuthenticated)
            //    appoints = appoints.Where(x => x.CreaterId != userId);
            var result = appoints.Select(x => new MapModel() { Id = x.Id, PeopleAmount = x.PeopleAmount, FoodTypes = x.FoodTypes, PriceRangeStart = x.PriceRangeStart, PriceRangeEnd = x.PriceRangeEnd, Name = x.Name, Latitude = x.Latitude, Longitude = x.Longitude, CreaterId = x.CreaterId, IsOver = x.IsOver }).Where(x => x.IsOver == false).ToList();
            if (!result.Any())
                return Json(new { status = "error", msg = "No data" }, JsonRequestBehavior.AllowGet);

            return Json(new { status = "success", data = result }, JsonRequestBehavior.AllowGet);
        }

        public class MapModel
        {
            public string Id { get; set; }
            public Nullable<int> PeopleAmount { get; set; }
            public string FoodTypes { get; set; }
            public Nullable<int> PriceRangeStart { get; set; }
            public Nullable<int> PriceRangeEnd { get; set; }
            public bool AllowAnonymous { get; set; }
            public Nullable<System.DateTime> CreatTimeUtc { get; set; }
            public Nullable<double> Latitude { get; set; }
            public Nullable<double> Longitude { get; set; }
            public string Name { get; set; }
            public string CreaterId { get; set; }
            public Nullable<System.DateTime> EatTime { get; set; }
            public bool IsOver { get; set; }
        }

        public ActionResult Detail(string id)
        {
            var data = db.Appointments.FirstOrDefault(x => x.Id == id);
            if (data == null)
                return HttpNotFound();

            string userId = User.Identity.GetUserId();
            if (String.Equals(userId, data.CreaterId, StringComparison.CurrentCultureIgnoreCase))
                return RedirectToAction("YesMan", new { id = data.Id });

            var accept = db.AppointmentAcceptions.FirstOrDefault(x => x.UserId == userId && x.AppointmentId == data.Id);
            if (accept != null)
                return RedirectToAction("YesMan", new { id = data.Id });

            int acceptCount = db.AppointmentAcceptions.Count(x => x.AppointmentId == data.Id) + 1;
            float acceptPersent = acceptCount / (float)data.PeopleAmount * 100;

            ViewBag.acceptCount = acceptCount;
            ViewBag.acceptPersent = acceptPersent;

            return View(data);
        }

        public ActionResult SayYes(string id)
        {
            var act = db.Appointments.FirstOrDefault(z => z.Id == id);
            if (act == null)
                return HttpNotFound();

            var acceptListCount = db.AppointmentAcceptions.Count(x => x.AppointmentId == act.Id);
            if (acceptListCount >= act.PeopleAmount - 1)
            {
                TempData["alert"] = "人數已滿";
                return RedirectToAction("Detail", new { id = act.Id });
            }

            var newAccept = new AppointmentAcception();
            newAccept.Id = Guid.NewGuid().ToString();
            newAccept.AppointmentId = act.Id;
            if (act.AllowAnonymous)
                newAccept.IsAccept = true;
            else
                newAccept.IsAccept = false;
            newAccept.CreatTimeUtc = DateTime.UtcNow;
            newAccept.UserId = User.Identity.GetUserId();
            db.AppointmentAcceptions.Add(newAccept);
            db.SaveChanges();

            return RedirectToAction("YesMan", new { id = act.Id });
        }

        public ActionResult YesMan(string id)
        {
            var act = db.Appointments.Include("AspNetUser").FirstOrDefault(z => z.Id == id);
            if (act == null)
                return HttpNotFound();

            string userId = User.Identity.GetUserId();
            if (userId.ToLower() != User.Identity.GetUserId().ToLower())
            {
                var accept =
                    db.AppointmentAcceptions.FirstOrDefault(x => x.UserId == userId && x.AppointmentId == act.Id);
                if (accept == null)
                    return HttpNotFound();
                if (userId != accept.UserId)
                    return HttpNotFound();
            }

            var acceptList = db.AppointmentAcceptions.Include("AspNetUser").Where(x => x.AppointmentId == act.Id).ToList();
            ViewBag.acceptList = acceptList;

            int acceptCount = db.AppointmentAcceptions.Count(x => x.AppointmentId == act.Id) + 1;
            float acceptPersent = acceptCount / (float)act.PeopleAmount * 100;

            ViewBag.acceptCount = acceptCount;
            ViewBag.acceptPersent = acceptPersent;

            return View(act);
        }

        public ActionResult RemoveAcception(string id)
        {
            var accept = db.AppointmentAcceptions.FirstOrDefault(x => x.Id == id);
            if (accept == null)
                return HttpNotFound();

            var act = db.Appointments.FirstOrDefault(x => x.Id == accept.AppointmentId);
            if (act == null)
                return HttpNotFound();

            db.AppointmentAcceptions.Remove(accept);
            db.SaveChanges();

            return RedirectToAction("YesMan", new { id = act.Id });
        }

        public ActionResult SayBye(string id)
        {
            var act = db.Appointments.FirstOrDefault(x => x.Id == id);
            if (act == null)
                return HttpNotFound();

            db.Entry(act).State = EntityState.Modified;
            act.IsOver = true;
            db.SaveChanges();

            return RedirectToAction("YesMan", new { id = act.Id });
        }

        public ActionResult SayGo(string id)
        {
            var act = db.Appointments.FirstOrDefault(x => x.Id == id);
            if (act == null)
                return HttpNotFound();

            db.Entry(act).State = EntityState.Modified;
            act.IsOver = true;
            act.IsGo = true;
            db.SaveChanges();

            return RedirectToAction("YesMan", new { id = act.Id });
        }
    }
}