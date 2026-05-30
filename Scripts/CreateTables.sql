-- PostgreSQL Database Creation Script for LendLedger standalone API
-- Targets PostgreSQL (compatible with local server and Neon DB)

-- 1. Create Lenders Table
CREATE TABLE IF NOT EXISTS public.lenders
(
    id UUID PRIMARY KEY,
    full_name VARCHAR(250) NOT NULL,
    email VARCHAR(200) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Index on Lender Email for faster authentication lookups
CREATE INDEX IF NOT EXISTS idx_lenders_email ON public.lenders(email);

-- 2. Create Borrowers Table
CREATE TABLE IF NOT EXISTS public.borrowers
(
    id UUID PRIMARY KEY,
    lender_id UUID NOT NULL,
    full_name VARCHAR(250) NOT NULL,
    phone VARCHAR(50) NOT NULL,
    email VARCHAR(200) NOT NULL,
    category VARCHAR(100) NOT NULL,
    auto_reminders BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_borrowers_lender FOREIGN KEY (lender_id)
        REFERENCES public.lenders(id) ON DELETE CASCADE
);

-- Index on LenderId for filtering borrowers by lender
CREATE INDEX IF NOT EXISTS idx_borrowers_lender_id ON public.borrowers(lender_id);

-- 3. Create Loans Table
CREATE TABLE IF NOT EXISTS public.loans
(
    id UUID PRIMARY KEY,
    borrower_id UUID NOT NULL UNIQUE,
    lender_id UUID NOT NULL,
    principal_amount NUMERIC(18,2) NOT NULL,
    remaining_balance NUMERIC(18,2) NOT NULL,
    emi_amount NUMERIC(18,2) NOT NULL,
    interest_rate NUMERIC(5,2) NOT NULL,
    interest_type VARCHAR(100) NOT NULL,
    repayment_cycle VARCHAR(100) NOT NULL,
    start_date TIMESTAMP WITH TIME ZONE NOT NULL,
    due_date TIMESTAMP WITH TIME ZONE NOT NULL,
    notes TEXT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'active',
    CONSTRAINT fk_loans_borrower FOREIGN KEY (borrower_id)
        REFERENCES public.borrowers(id) ON DELETE CASCADE,
    CONSTRAINT fk_loans_lender FOREIGN KEY (lender_id)
        REFERENCES public.lenders(id) ON DELETE RESTRICT
);

-- Index on BorrowerId (already unique) and LenderId
CREATE INDEX IF NOT EXISTS idx_loans_lender_id ON public.loans(lender_id);

-- 4. Create Payments Table
CREATE TABLE IF NOT EXISTS public.payments
(
    id UUID PRIMARY KEY,
    borrower_id UUID NOT NULL,
    lender_id UUID NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    date_received TIMESTAMP WITH TIME ZONE NOT NULL,
    method VARCHAR(100) NOT NULL,
    reference_id VARCHAR(200) NULL,
    notes TEXT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_payments_borrower FOREIGN KEY (borrower_id)
        REFERENCES public.borrowers(id) ON DELETE CASCADE,
    CONSTRAINT fk_payments_lender FOREIGN KEY (lender_id)
        REFERENCES public.lenders(id) ON DELETE RESTRICT
);

-- Indexes for payments queries
CREATE INDEX IF NOT EXISTS idx_payments_borrower_id ON public.payments(borrower_id);
CREATE INDEX IF NOT EXISTS idx_payments_lender_id ON public.payments(lender_id);

-- 5. Create Notes Table
CREATE TABLE IF NOT EXISTS public.notes
(
    id UUID PRIMARY KEY,
    borrower_id UUID NOT NULL,
    lender_id UUID NOT NULL,
    content TEXT NOT NULL,
    date_added TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_notes_borrower FOREIGN KEY (borrower_id)
        REFERENCES public.borrowers(id) ON DELETE CASCADE,
    CONSTRAINT fk_notes_lender FOREIGN KEY (lender_id)
        REFERENCES public.lenders(id) ON DELETE RESTRICT
);

-- Indexes for notes queries
CREATE INDEX IF NOT EXISTS idx_notes_borrower_id ON public.notes(borrower_id);
CREATE INDEX IF NOT EXISTS idx_notes_lender_id ON public.notes(lender_id);

-- 6. Create Lookup Values Table
CREATE TABLE IF NOT EXISTS public.lookup_values
(
    id VARCHAR(100) PRIMARY KEY,
    type VARCHAR(50) NOT NULL,
    code VARCHAR(50) NOT NULL,
    value VARCHAR(100) NOT NULL,
    description VARCHAR(250) NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- Index on Type for fast filtering
CREATE INDEX IF NOT EXISTS idx_lookup_values_type ON public.lookup_values(type);

-- Seed initial lookup values
INSERT INTO public.lookup_values (id, type, code, value, description, is_active)
VALUES
    ('category:personal', 'Category', 'personal', 'Personal', 'Personal expenses, medical, travel, etc.', TRUE),
    ('category:business', 'Category', 'business', 'Business', 'Business operation or capital needs', TRUE),
    ('category:emergency', 'Category', 'emergency', 'Emergency', 'Immediate financial crisis or medical emergencies', TRUE),
    ('category:education', 'Category', 'education', 'Education', 'Tuition, academic books, and college fees', TRUE),
    ('category:equipment', 'Category', 'equipment', 'Equipment', 'Tools, machinery, or computing equipment leasing', TRUE),

    ('interest_type:flat', 'InterestType', 'flat', 'Flat', 'Flat interest rate calculated on principal amount', TRUE),
    ('interest_type:reducing', 'InterestType', 'reducing', 'Reducing', 'Reducing balance interest rate', TRUE),

    ('payment_method:cash', 'PaymentMethod', 'cash', 'Cash', 'Hand-delivered cash payments', TRUE),
    ('payment_method:bank_transfer', 'PaymentMethod', 'bank_transfer', 'Bank Transfer', 'Direct wire or ACH bank transfers', TRUE),
    ('payment_method:check', 'PaymentMethod', 'check', 'Check', 'Physical paper checks', TRUE),
    ('payment_method:online', 'PaymentMethod', 'online', 'Online', 'Digital app transfers (Zelle, Venmo, PayPal, etc.)', TRUE),

    ('loan_status:active', 'LoanStatus', 'active', 'Active', 'Active loan with balance pending', TRUE),
    ('loan_status:overdue', 'LoanStatus', 'overdue', 'Overdue', 'Payment schedule is late or unpaid', TRUE),
    ('loan_status:paid', 'LoanStatus', 'paid', 'Paid', 'Fully settled and closed loan', TRUE),

    ('repayment_cycle:weekly', 'RepaymentCycle', 'weekly', 'Weekly', 'Installments paid once a week', TRUE),
    ('repayment_cycle:biweekly', 'RepaymentCycle', 'biweekly', 'Bi-Weekly', 'Installments paid every two weeks', TRUE),
    ('repayment_cycle:monthly', 'RepaymentCycle', 'monthly', 'Monthly', 'Installments paid once a month', TRUE)
ON CONFLICT (id) DO UPDATE 
SET type = EXCLUDED.type,
    code = EXCLUDED.code,
    value = EXCLUDED.value,
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active;
