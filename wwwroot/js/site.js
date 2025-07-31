// Cart functionality
document.addEventListener('DOMContentLoaded', function() {
    // Initialize cart count
    let cartCount = 0;
    const cartLink = document.querySelector('nav a[href="#"]') || document.querySelector('nav a:last-child');
    
    // Update cart display
    function updateCartDisplay() {
        if (cartLink) {
            cartLink.textContent = `Cart (${cartCount})`;
        }
    }
    
    // Add to cart buttons
    const addToCartButtons = document.querySelectorAll('.product-card');
    addToCartButtons.forEach(card => {
        const button = document.createElement('button');
        button.textContent = 'Add to Cart';
        button.className = 'add-to-cart-btn';
        button.addEventListener('click', function() {
            cartCount++;
            updateCartDisplay();
            alert('Product added to cart!');
        });
        card.appendChild(button);
    });
    
    // Initial cart display update
    updateCartDisplay();
});

I've added JavaScript functionality to handle product interactions:
1. Added "Add to Cart" buttons to each product card
2. Implemented a cart counter that updates when products are added
3. Added visual feedback when adding items to cart

Would you like me to add any additional features like:
- Product filtering/sorting
- Cart persistence using localStorage
- Product detail modals
- Price formatting functionality?