using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CitizenFX.Core;

using MenuAPI;

using Newtonsoft.Json;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;
using static vMenuShared.PermissionsManager;

namespace vMenuClient.menus
{
    /// <summary>
    /// LEO Vehicles menu.
    /// Creates a "LEO Vehicles" page with a sub-menu for each department,
    /// loaded from config/leo_vehicles.json so it can be edited without recompiling.
    /// </summary>
    public class LEOVehicles
    {
        private Menu menu;

        public bool SpawnInVehicle { get; private set; } = UserDefaults.VehicleSpawnerSpawnInside;
        public bool ReplaceVehicle { get; private set; } = UserDefaults.VehicleSpawnerReplacePrevious;

        /// <summary>
        /// One vehicle entry as it appears in config/leo_vehicles.json.
        /// </summary>
        public class LeoVehicleEntry
        {
            public string Name { get; set; }
            public string Model { get; set; }
        }

        /// <summary>
        /// Loads config/leo_vehicles.json.
        /// JSON shape:
        /// {
        ///   "LSPD - Los Santos Police": [
        ///     { "Name": "Police Cruiser (Interceptor)", "Model": "police" },
        ///     { "Name": "Police Bike", "Model": "policeb" }
        ///   ],
        ///   "BCSO - Blaine County Sheriff": [
        ///     { "Name": "Sheriff Cruiser", "Model": "sheriff" }
        ///   ]
        /// }
        /// </summary>
        public static Dictionary<string, List<LeoVehicleEntry>> LoadDepartments()
        {
            try
            {
                var jsonData = LoadResourceFile(GetCurrentResourceName(), "config/leo_vehicles.json") ?? "{}";
                var data = JsonConvert.DeserializeObject<Dictionary<string, List<LeoVehicleEntry>>>(jsonData);
                return data ?? new Dictionary<string, List<LeoVehicleEntry>>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"^1[vMenu] [LEOVehicles] Failed to load or parse config/leo_vehicles.json: {ex.Message}^7");
                return new Dictionary<string, List<LeoVehicleEntry>>();
            }
        }

        private void CreateMenu()
        {
            menu = new Menu(Game.Player.Name, "LEO Vehicles");

            var spawnInVeh = new MenuCheckboxItem("Spawn Inside Vehicle", "This will teleport you into the vehicle when you spawn it.", SpawnInVehicle);
            var replacePrev = new MenuCheckboxItem("Replace Previous Vehicle", "This will automatically delete your previously spawned vehicle when you spawn a new vehicle.", ReplaceVehicle);
            menu.AddMenuItem(spawnInVeh);
            menu.AddMenuItem(replacePrev);

            menu.OnCheckboxChange += (sender, item, index, _checked) =>
            {
                if (item == spawnInVeh)
                {
                    SpawnInVehicle = _checked;
                }
                else if (item == replacePrev)
                {
                    ReplaceVehicle = _checked;
                }
            };

            var departments = LoadDepartments();

            if (departments.Count == 0)
            {
                var noneBtn = new MenuItem("No departments configured", "Add departments and vehicles to config/leo_vehicles.json on the server.")
                {
                    Enabled = false,
                    LeftIcon = MenuItem.Icon.LOCK,
                };
                menu.AddMenuItem(noneBtn);
                return;
            }

            // Build one sub-menu per department.
            foreach (var department in departments)
            {
                var deptName = department.Key;
                var vehicles = department.Value ?? new List<LeoVehicleEntry>();

                var deptMenu = new Menu(Game.Player.Name, deptName);
                var deptBtn = new MenuItem(deptName, $"Vehicles available to {deptName}.") { Label = "→→→" };

                menu.AddMenuItem(deptBtn);
                MenuController.AddSubmenu(menu, deptMenu);
                MenuController.BindMenuItem(menu, deptMenu, deptBtn);

                foreach (var entry in vehicles)
                {
                    if (string.IsNullOrWhiteSpace(entry?.Model))
                    {
                        continue;
                    }

                    var displayName = string.IsNullOrWhiteSpace(entry.Name) ? entry.Model : entry.Name;
                    var model = entry.Model;
                    var exists = DoesModelExist(model);

                    var vehBtn = new MenuItem(displayName, exists
                        ? $"Click to spawn a {displayName}."
                        : "This vehicle model could not be found. Make sure it's a valid model name or that the addon is being streamed by the server.")
                    {
                        Label = $"({model})",
                        ItemData = model,
                        Enabled = exists,
                    };

                    if (!exists)
                    {
                        vehBtn.LeftIcon = MenuItem.Icon.LOCK;
                    }

                    deptMenu.AddMenuItem(vehBtn);
                }

                deptMenu.OnItemSelect += async (sender, item, index) =>
                {
                    if (item.ItemData is string modelName)
                    {
                        await SpawnVehicle(modelName, SpawnInVehicle, ReplaceVehicle);
                    }
                };

                if (deptMenu.Size == 0)
                {
                    deptBtn.Enabled = false;
                    deptBtn.LeftIcon = MenuItem.Icon.LOCK;
                    deptBtn.Description = "There are no vehicles configured for this department.";
                }
            }
        }

        /// <summary>
        /// Create the menu if it doesn't exist, and then returns it.
        /// </summary>
        /// <returns>The Menu</returns>
        public Menu GetMenu()
        {
            if (menu == null)
            {
                CreateMenu();
            }
            return menu;
        }
    }
}
